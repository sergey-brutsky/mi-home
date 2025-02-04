namespace MiHomeLib.Commands;

internal class DiscoverGatewayCommand: Command
{
    public override string ToString()
    {
        return "{\"cmd\":\"get_id_list\"}";
    }
}