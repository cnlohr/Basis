using Basis.Scripts.Networking.NetworkedAvatar;
public static class BasisUtilities
{
    public static bool IsValid(BasisNetworkPlayer Player)
    {
        if(Player == null)
        {
            return false;
        }

        return true;
    }
}
