namespace MiHomeLib.MultimodeGateway.JsonResponses;

public class GetDevicePropResponse : MiioResponse
{
    public int Code { get; set; }
    public int Id { get; set; }
    public int[] Result { get; set; }
    public int ExeTime { get; set; }
    public MiioError Error { get; set; }

    public class MiioError
    {
        public int Code { get; set; }
        public string Message { get; set; }
    }
}
