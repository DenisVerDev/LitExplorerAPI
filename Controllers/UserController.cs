using LitExplorerAPI.LitExplorerDTO;
using LitExplorerAPI.LitExplorerModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                
                var userDb = await litExplorerContext.Users.FirstOrDefaultAsync(u => u.Email == userDTO.Email);
                if(userDb == null)
                {
                    userDb = new User() { Email = userDTO.Email, HashedPassword=userDTO.HashedPassword };
                    
                    await litExplorerContext.Users.AddAsync(userDb);
                    await litExplorerContext.SaveChangesAsync();

                    userDTO.UserId = userDb.UserId;
                    userDTO.RegistrationDate = userDb.RegistrationDate;

                    return Ok(userDTO);
                }

                return BadRequest("Such user is already registered!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while registering new user", Error = ex.Message });
            }
        }

        [HttpPost("signIn")]
        public async Task<IActionResult> SignIn([FromBody] UserDTO userDTO)
        {
            try
            {
                if (userDTO == null)
                    return BadRequest("Received user was null!");

                var userDb = await litExplorerContext.Users.FirstOrDefaultAsync(u => u.Email == userDTO.Email);
                if (userDb != null)
                {
                    userDTO.UserId = userDb.UserId;
                    userDTO.RegistrationDate = userDb.RegistrationDate;

                    return userDb.HashedPassword == userDTO.HashedPassword ? Ok(userDTO) : BadRequest("Wrong password!");
                }

                return BadRequest("Acount under provided email address doesn't exist!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while signing up", Error = ex.Message });
            }
        }
    }
}
