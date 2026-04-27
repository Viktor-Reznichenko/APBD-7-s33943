using Tutorial7.DTOs;

namespace Tutorial7.Services;

public interface IAppointmentsService
{
    Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName);
    Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int idAppointment);
    Task<AppointmentDetailsDto> CreateAppointmentAsync(CreateAppointmentRequestDto dto);
    Task<AppointmentDetailsDto> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto dto);
    Task DeleteAppointmentAsync(int idAppointment);
}