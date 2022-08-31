namespace LuckyRabbitsFoot
{
    public class Config
    {
        public double QualityMultiplier { get; set; }

        public int BaseLuckValue { get; set; }

        public bool ApplyCountMultiplier { get; set; }

        public Config()
        {
            QualityMultiplier = 1.0;
            BaseLuckValue = 1;
        }

        public Config(double defaultMultiplier) => QualityMultiplier = defaultMultiplier;

        public Config(double qualityMultiplier, int baseLuckValue) : this(qualityMultiplier)
        {
            BaseLuckValue = baseLuckValue;
        }

        public Config(double qualityMultiplier, int baseLuckValue, bool applyCountMultiplier) : this(qualityMultiplier, baseLuckValue)
        {
            ApplyCountMultiplier = applyCountMultiplier;
        }
    }
}
