namespace MiHomeLib.Devices
{
    public class RadioChannel
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public int Type { get; set; }

        public override string ToString()
        {
            return $"Radio channel -> Id: {Id}, Url: {Url}";
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            RadioChannel other = (RadioChannel)obj;

            return Id == other.Id && Url == other.Url;
        }

        public override int GetHashCode()
        {
            return new { Id, Url }.GetHashCode();
        }
    }
}