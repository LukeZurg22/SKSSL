#pragma warning disable CS0169 // Field is never used
namespace SKSSL.Networking;

public class UserClient // IMPL: No networking has been achieved as of 20251215. One day...!
{
    private readonly ISettings _settings;

    public UserClient(ISettings settings)
    {
        _settings = settings;
    }
    
    /// <summary>Updates client information.</summary>
    public void Update()
    {
        
    }
}