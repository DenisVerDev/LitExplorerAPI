namespace LitExplorerAPI.LitExplorerDTO
{
    public class UserDTO
    {
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public DateTime RegistrationDate { get; set; }
    }
}
