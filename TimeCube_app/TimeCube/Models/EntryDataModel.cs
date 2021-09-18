namespace TimeCube.Models
{
    public class EntryDataModel
    {
        public string PortName { get; set; }
        public string LeftWallName { get; set; }
        public string RightWallName { get; set; }
        public string UpperWallName { get; set; }
        public string LowerWallName { get; set; }
        public string BreakWallName { get { return "Coffee time"; } }
        public string OutputDirectory { get; set; }
    }
}
