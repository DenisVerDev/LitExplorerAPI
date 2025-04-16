using LitExplorerAPI.LitExplorerDTO;
using LitExplorerAPI.LitExplorerModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LitExplorerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly LitExplorerContext litExplorerContext;

        public UserController(LitExplorerContext litExplorerContext)
            => this.litExplorerContext = litExplorerContext;

        [HttpPost("signUp")]
        public async Task<IActionResult> SignUp([FromBody] UserDTO userDTO)
        {
            try
            {
                if (userDTO == null)
                    return BadRequest("Received user was null!");

                if(userDTO.Email.IsNullOrEmpty() || userDTO.Password.IsNullOrEmpty())
                    return BadRequest("Received user data was empty!");

                var userDb = await litExplorerContext.Users.FirstOrDefaultAsync(u => u.Email == userDTO.Email);
                if(userDb == null)
                {
                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(userDTO.Password, workFactor:12);
                    userDb = new User() { Email = userDTO.Email, HashedPassword = hashedPassword };
                    
                    await litExplorerContext.Users.AddAsync(userDb);
                    await litExplorerContext.SaveChangesAsync();

                    userDTO.UserId = userDb.UserId;
                    userDTO.RegistrationDate = userDb.RegistrationDate;

                    return Ok(userDTO);
                }

                return Conflict("Such user is already registered!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while signing up", Error = ex.Message });
            }
        }

        [HttpPost("signIn")]
        public async Task<IActionResult> SignIn([FromBody] UserDTO userDTO)
        {
            try
            {
                if (userDTO == null)
                    return BadRequest("Received user was null!");

                if (userDTO.Email.IsNullOrEmpty() || userDTO.Password.IsNullOrEmpty())
                    return BadRequest("Received user data was empty!");

                var userDb = await litExplorerContext.Users.FirstOrDefaultAsync(u => u.Email == userDTO.Email);
                if (userDb != null)
                {
                    userDTO.UserId = userDb.UserId;
                    userDTO.RegistrationDate = userDb.RegistrationDate;

                    return BCrypt.Net.BCrypt.Verify(userDTO.Password, userDb.HashedPassword) ? Ok(userDTO) : BadRequest("Wrong password!");
                }

                return Conflict("Acount under provided email address doesn't exist!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while signing in", Error = ex.Message });
            }
        }
    }
}
