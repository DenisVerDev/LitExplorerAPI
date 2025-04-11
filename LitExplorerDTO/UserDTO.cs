namespace LitExplorerAPI.LitExplorerDTO
{
    public class UserDTO
    {
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public string HashedPassword { get; set; } = null!;
        public DateTime RegistrationDate { get; set; }
    }
}
