using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tutorial7.DTOs;
using Tutorial7.Services;

namespace Tutorial7.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        private readonly IAppointmentsService _appointmentsService;

        public AppointmentsController(IAppointmentsService appointmentsService)
        {
            _appointmentsService = appointmentsService;
        }
        
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? status, [FromQuery] string? patientLastName)
        {
            var appointments = await _appointmentsService.GetAppointmentsAsync(status, patientLastName);
            return Ok(appointments);
        }

        [HttpGet("{idAppointment:int}")]
        public async Task<IActionResult> GetById(int idAppointment)
        {
            var appointment = await _appointmentsService.GetAppointmentByIdAsync(idAppointment);
            if (appointment == null)
            {
                return NotFound(new ErrorResponseDto { Message = "Appointment not found." });
            }
            return Ok(appointment);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentRequestDto request)
        {
            try
            {
                var created = await _appointmentsService.CreateAppointmentAsync(request);
                return CreatedAtAction(nameof(GetById), new { idAppointment = created.IdAppointment }, created);
            }
            catch (AppointmentException ex)
            {
                return StatusCode(ex.StatusCode, new ErrorResponseDto { Message = ex.Message });
            }
        }

        [HttpPut("{idAppointment:int}")]
        public async Task<IActionResult> Update(int idAppointment, [FromBody] UpdateAppointmentRequestDto request)
        {
            try
            {
                var updated = await _appointmentsService.UpdateAppointmentAsync(idAppointment, request);
                return Ok(updated);
            }
            catch (AppointmentException ex)
            {
                return StatusCode(ex.StatusCode, new ErrorResponseDto { Message = ex.Message });
            }
        }

        [HttpDelete("{idAppointment:int}")]
        public async Task<IActionResult> Delete(int idAppointment)
        {
            try
            {
                await _appointmentsService.DeleteAppointmentAsync(idAppointment);
                return NoContent();
            }
            catch (AppointmentException ex)
            {
                return StatusCode(ex.StatusCode, new ErrorResponseDto { Message = ex.Message });
            }
        }
    }
}
