namespace Application.Common
{
    /// <summary>
    /// Whether a new customer must enter an activation code before signing in.
    /// Off for now: there is no SMS/email provider yet, so customers are active
    /// as soon as they register. Set to true again once a provider is wired in.
    /// </summary>
    public static class AccountActivation
    {
        public static readonly bool CustomerRequiresCode = false;
    }
}
