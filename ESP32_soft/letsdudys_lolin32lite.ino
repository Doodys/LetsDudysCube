#include <Wire.h>
#include <LSM6.h>
//#include "BluetoothSerial.h"
#include <WiFi.h>

#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>

//use SDA and SCL defines if you'd like to use other ESP32 board than ESP32 DevktiC
//then, instead of Wire.begin() in setup() use Wire.begin(I2C_SDA, I2C_SCL);
#define I2C_SDA 23
#define I2C_SCL 19

#define PERIOD 1*1000L

//BluetoothSerial SerialBT;
LSM6 imu;

const int pin1 = 25;
const int pin2 = 26;
const int pin3 = 33;
const int pin4 = 27;
const int pin5 = 32;
const int deepSleepButton = 15; 
int counter = 0;

unsigned long keyPrevMillis = 0;
const unsigned long keySampleIntervalMs = 25;
byte longKeyPressCountMax = 80;    // 80 * 25 = 2000 ms
byte longKeyPressCount = 0;
byte prevKeyState = HIGH;  

unsigned long target_time = 0L ;

BLEServer* pServer = NULL;
BLECharacteristic* pCharacteristic = NULL;
bool deviceConnected = false;
bool oldDeviceConnected = false;

// See the following for generating UUIDs:
// https://www.uuidgenerator.net/

#define SERVICE_UUID        "4fafc201-1fb5-459e-8fcc-c5c9c331914b"
#define CHARACTERISTIC_UUID "beb5483e-36e1-4688-b7f5-ea07361b26a8"


class MyServerCallbacks: public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) {
      deviceConnected = true;
    };

    void onDisconnect(BLEServer* pServer) {
      deviceConnected = false;
    }
};

void setup()
{
  setCpuFrequencyMhz(60); 
  Serial.begin(115200);
  WiFi.setSleep(true);
  esp_sleep_enable_ext0_wakeup(GPIO_NUM_15, 1);
  Wire.begin(I2C_SDA, I2C_SCL); //uncomment for other ESP32 boards thank DevkitC
  //Wire.begin();
  //SerialBT.begin("LetsDudysCube"); //Bluetooth device name
  if (!imu.init())
  {
    Serial.println("Failed to detect and initialize IMU!");
    while (1);
  }
  imu.enableDefault();

  // Create the BLE Device
  BLEDevice::init("LetsDudysCube2");

  // Create the BLE Server
  pServer = BLEDevice::createServer();
  pServer->setCallbacks(new MyServerCallbacks());

  // Create the BLE Service
  BLEService *pService = pServer->createService(SERVICE_UUID);

  // Create a BLE Characteristic
  pCharacteristic = pService->createCharacteristic(
                      CHARACTERISTIC_UUID,
                      BLECharacteristic::PROPERTY_READ   |
                      BLECharacteristic::PROPERTY_WRITE  |
                      BLECharacteristic::PROPERTY_NOTIFY |
                      BLECharacteristic::PROPERTY_INDICATE
                    );

  // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.descriptor.gatt.client_characteristic_configuration.xml
  // Create a BLE Descriptor
  pCharacteristic->addDescriptor(new BLE2902());

  // Start the service
  pService->start();

  // Start advertising
  BLEAdvertising *pAdvertising = BLEDevice::getAdvertising();
  pAdvertising->addServiceUUID(SERVICE_UUID);
  pAdvertising->setScanResponse(true);
  pAdvertising->setMinPreferred(0x06);  // functions that help with iPhone connections issue
  pAdvertising->setMinPreferred(0x12);
  BLEDevice::startAdvertising();

  pinMode(pin1,OUTPUT);
  pinMode(pin2,OUTPUT);
  pinMode(pin3,OUTPUT);
  pinMode(pin4,OUTPUT);
  pinMode(pin5,OUTPUT);

  digitalWrite(pin2,HIGH);
  digitalWrite(pin2,HIGH);
  digitalWrite(pin3,HIGH);
  digitalWrite(pin4,HIGH);
  digitalWrite(pin5,HIGH);

  pinMode(deepSleepButton, INPUT);
}

uint8_t turnOnLed(int x, int y, int z)
{
  digitalWrite(pin2,LOW);
  digitalWrite(pin2,LOW);
  digitalWrite(pin3,LOW);
  digitalWrite(pin4,LOW);
  digitalWrite(pin5,LOW);
  
  if (z >= 1000 && z <= 1100){
    digitalWrite(pin1,HIGH);
    //return "UP";
    return 1;
  }
  else if (z >= -1000 && z <= -800){
    digitalWrite(pin3,HIGH);
    //return "DOWN";
    return 2;
  } 
  else if (x >= -1000 && x <= -800){
    //return "FRONT";
    return 3;
  } 
  else if (x >= 1000 && x <= 1100){
    digitalWrite(pin4,HIGH);
    //return "BACK";
    return 4;
  }
  else if (y >= 1000 && y <= 1100){
    digitalWrite(pin5,HIGH);
    //return "RIGHT";
    return 5;
  } 
  else if (y >= -1000 && y <= -800){
    digitalWrite(pin2,HIGH);
    //return "LEFT";
    return 6;
  }
}

uint8_t readPosition(){
  imu.read();
  return turnOnLed(imu.a.x * 0.061,imu.a.y * 0.061, imu.a.z * 0.061);
}

// called when button is kept pressed for less than 2 seconds
// not in use atm
void shortKeyPress() {  
}


// called when button is kept pressed for more than 2 seconds
void longKeyPress() {
    esp_deep_sleep_start();
}


// called when key goes from not pressed to pressed
void keyPress() {
    longKeyPressCount = 0;
}


// called when key goes from pressed to not pressed
void keyRelease() {
    if (longKeyPressCount >= longKeyPressCountMax) {
        longKeyPress();
    }
    else {
        shortKeyPress();
    }
}

void loop()
{
  // key management section
  if (millis() - keyPrevMillis >= keySampleIntervalMs) {
      keyPrevMillis = millis();
      
      byte currKeyState = digitalRead(deepSleepButton);
      
      if ((prevKeyState == LOW) && (currKeyState == HIGH)) {
          keyPress();
      }
      else if ((prevKeyState == HIGH) && (currKeyState == LOW)) {
          keyRelease();
      }
      else if (currKeyState == HIGH) {
          longKeyPressCount++;
      }
      
      prevKeyState = currKeyState;
  }

// notify changed value
    if (deviceConnected) {
        if (millis () - target_time >= PERIOD)
        {
          target_time += PERIOD ;
          uint8_t value = readPosition();
          pCharacteristic->setValue((uint8_t*)&value, 1);
          pCharacteristic->notify();
          delay(3);
          //SerialBT.println(readPosition());
        }
          // bluetooth stack will go into congestion, if too many packets are sent, in 6 hours test i was able to go as low as 3ms
    }
    // disconnecting
    if (!deviceConnected && oldDeviceConnected) {
        delay(500); // give the bluetooth stack the chance to get things ready
        pServer->startAdvertising(); // restart advertising
        oldDeviceConnected = deviceConnected;
        ESP.restart();
    }
    // connecting
    if (deviceConnected && !oldDeviceConnected) {
        // do stuff here on connecting
        oldDeviceConnected = deviceConnected;
    }
}
