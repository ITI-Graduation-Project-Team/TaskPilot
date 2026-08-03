using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using TaskPilot.Services.Interfaces.External; 
using System;
using System.Threading.Tasks;

namespace TaskPilot.Presentation.Controllers;

[Route("api/[controller]")]
[ApiController]
public class GoogleCalendarIntegrationController : ControllerBase
{
    private readonly IGoogleCalendarService _calendarService;

    public GoogleCalendarIntegrationController(IGoogleCalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    [HttpGet("connect")]
    [Authorize] 
    public IActionResult ConnectGoogleCalendar()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out Guid userId))
            return Unauthorized("لا يمكن التعرف على المستخدم.");

        var url = _calendarService.GetGoogleLoginUrl(userId);
        return Ok(new { Url = url });
    }

    [AllowAnonymous] 
    [HttpGet("callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrEmpty(code) || !Guid.TryParse(state, out Guid userId))
            return BadRequest("طلب غير صالح أو بيانات مفقودة.");

        var success = await _calendarService.ExchangeCodeForTokenAsync(code, userId);

        if (success)
            return Ok("تم ربط تقويم جوجل بنجاح! يمكنك إغلاق هذه النافذة والعودة للمنصة.");
        else
            return BadRequest("حدث خطأ أثناء محاولة ربط الحساب. قد يكون الكود منتهي الصلاحية.");
    }
}



//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Authorization;
//using System.Security.Claims;
//using TaskPilot.Infrastructure.Services.Google;
//using System.Net;

//namespace TaskPilot.Presentation.Controllers;

//[Route("api/[controller]")]
//[ApiController]
//[Authorize]
//public class GoogleCalendarIntegrationController : ControllerBase // <--- لاحظي تغيير الاسم هنا
//{
//    private readonly IGoogleCalendarService _calendarService;

//    public GoogleCalendarIntegrationController(IGoogleCalendarService calendarService) // <--- وهنا أيضاً
//    {
//        _calendarService = calendarService;
//    }

//    [HttpGet("connect")]
//    public IActionResult ConnectGoogleCalendar()
//    {

//        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
//        if (!Guid.TryParse(userIdString, out Guid userId))
//           return Unauthorized("لا يمكن التعرف على المستخدم.");
//        var url = _calendarService.GetGoogleLoginUrl();
//        return Ok(new { Url = url });
//    }

//    [AllowAnonymous]
//    [HttpGet("callback")]
//    public async Task<IActionResult> GoogleCallback([FromQuery] string code, [FromQuery] string state)
//    {
//        var decodedCode = WebUtility.UrlDecode(code);

//        if (string.IsNullOrEmpty(code))
//            return BadRequest("لم يتم استلام الكود من جوجل.");

//        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
//        if (!Guid.TryParse(userIdString, out Guid userId))
//            return Unauthorized("لا يمكن التعرف على المستخدم.");

//        var success = await _calendarService.ExchangeCodeForTokenAsync(decodedCode, userId);

//        if (success)
//            return Ok("تم ربط تقويم جوجل بنجاح! يمكنك إغلاق هذه النافذة والعودة للمنصة.");
//        else
//            return BadRequest("حدث خطأ أثناء محاولة ربط الحساب.");
//    }
//}