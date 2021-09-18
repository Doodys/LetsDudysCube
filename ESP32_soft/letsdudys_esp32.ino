#include <Wire.h>
#include <LSM6.h>
#include "BluetoothSerial.h"
#include <pgmspace.h>
#include <WiFi.h>

//use SDA and SCL defines if you'd like to use other ESP32 board than ESP32 DevktiC
//then, instead of Wire.begin() in setup() use Wire.begin(I2C_SDA, I2C_SCL);
#define I2C_SDA 23
#define I2C_SCL 19

#define PERIOD 1*1000L

BluetoothSerial SerialBT;
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

void setup()
{
  setCpuFrequencyMhz(60); 
  Serial.begin(115200);
  WiFi.setSleep(true);
  esp_sleep_enable_ext0_wakeup(GPIO_NUM_15, 1);
  //Wire.begin(I2C_SDA, I2C_SCL); //uncomment for other ESP32 boards thank DevkitC
  Wire.begin();
  SerialBT.begin("LetsDudysCube"); //Bluetooth device name
  if (!imu.init())
  {
    SerialBT.println(F("Failed to detect and initialize IMU!"));
    while (1);
  }
  imu.enableDefault();
  
  pinMode(pin1,OUTPUT);
  pinMode(pin2,OUTPUT);
  pinMode(pin3,OUTPUT);
  pinMode(pin4,OUTPUT);
  pinMode(pin5,OUTPUT);

  pinMode(deepSleepButton, INPUT);
}

String turnOnLed(int x, int y, int z)
{
  digitalWrite(pin2,LOW);
  digitalWrite(pin2,LOW);
  digitalWrite(pin3,LOW);
  digitalWrite(pin4,LOW);
  digitalWrite(pin5,LOW);
  if (z >= 1000 && z <= 1100){
    digitalWrite(pin1,HIGH);
    return "UP";
  }
  else if (z >= -1000 && z <= -800){
    digitalWrite(pin3,HIGH);
    return "DOWN";
  } 
  else if (x >= -1000 && x <= -800){
    return "FRONT";
  } 
  else if (x >= 1000 && x <= 1100){
    digitalWrite(pin4,HIGH);
    return "BACK";
  }
  else if (y >= 1000 && y <= 1100){
    digitalWrite(pin5,HIGH);
    return "RIGHT";
  } 
  else if (y >= -1000 && y <= -800){
    digitalWrite(pin2,HIGH);
    return "LEFT";
  }
}

String readPosition(){
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

  //counter = counter + 1;
  //delay(1000);
  //SerialBT.println(counter);

  if (millis () - target_time >= PERIOD)
  {
    target_time += PERIOD ;
    SerialBT.println(readPosition());
  }
  
  
}
