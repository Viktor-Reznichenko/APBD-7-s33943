using Microsoft.Data.SqlClient;
using Tutorial7.DTOs;

namespace Tutorial7.Services;

public class AppointmentsService : IAppointmentsService
{
    private readonly string _connectionString;
    public AppointmentsService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("error");
    }
    
    
    
    
    public async Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName)
    {
        var query = """
                    SELECT
                        a.IdAppointment,
                        a.AppointmentDate,
                        a.Status,
                        a.Reason,
                        p.FirstName + N' ' + p.LastName AS PatientFullName,
                        p.Email AS PatientEmail
                    FROM dbo.Appointments a
                    JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                    WHERE (@Status IS NULL OR a.Status = @Status)
                      AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                    ORDER BY a.AppointmentDate;
                    """;
        
        await using var connection = new SqlConnection(_connectionString);
        
        await connection.OpenAsync();

        
        
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue(
            "@Status",
            string.IsNullOrWhiteSpace(status) ? DBNull.Value : status
        );
        command.Parameters.AddWithValue(
            "@PatientLastName",
            string.IsNullOrWhiteSpace(patientLastName) ? DBNull.Value : patientLastName
        );
        var reader = await command.ExecuteReaderAsync();
        var appointments = new List<AppointmentListDto>();
        while (await reader.ReadAsync())
        {
            var appointment = new AppointmentListDto()
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                PatientFullName = reader.GetString(4),
                PatientEmail = reader.GetString(5)
            };
            appointments.Add(appointment);
        }
        return appointments;
    }

    public async Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int idAppointment)
    {
        var query = """
                    SELECT
                        a.IdAppointment,
                        a.AppointmentDate,
                        a.Status,
                        a.Reason,
                        a.InternalNotes,
                        a.CreatedAt,
                        p.IdPatient,
                        p.FirstName + N' ' + p.LastName AS PatientFullName,
                        p.Email AS PatientEmail,
                        p.PhoneNumber as PatientPhoneNumber,
                        d.IdDoctor,
                        d.FirstName + N' ' + d.LastName AS DoctorFullName,
                        d.LicenseNumber as DoctorLicenseNumber,
                        s.Name as DoctorSpecialization
                    FROM dbo.Appointments a
                    JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                    JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
                    JOIN dbo.Specializations s ON d.IdSpecialization = s.IdSpecialization
                    WHERE a.IdAppointment = @IdAppointment;
                    """;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@IdAppointment", idAppointment);
        var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AppointmentDetailsDto()
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                InternalNotes = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                IdPatient = reader.GetInt32(6),
                PatientFullName = reader.GetString(7),
                PatientEmail = reader.GetString(8),
                PatientPhoneNumber = reader.GetString(9),
                IdDoctor = reader.GetInt32(10),
                DoctorFullName = reader.GetString(11),
                DoctorLicenseNumber = reader.GetString(12),
                DoctorSpecialization = reader.GetString(13)
            };
        }
        return null;
    }

    public async Task<AppointmentDetailsDto> CreateAppointmentAsync(CreateAppointmentRequestDto dto)
    {
        if (dto.AppointmentDate < DateTime.UtcNow)
        {
            throw new AppointmentException(StatusCodes.Status400BadRequest, "date must not be in the past");
        }
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await ValidatePatient(connection, dto.IdPatient);
        await ValidateDoctor(connection, dto.IdDoctor);
        await ValidateDate(connection, dto.IdDoctor, dto.AppointmentDate, null);

        var sql = """
                  INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
                  OUTPUT INSERTED.IdAppointment
                  VALUES (@IdPatient, @IdDoctor, @AppointmentDate, N'Scheduled', @Reason);
                  """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
        command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
        command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
        command.Parameters.AddWithValue("@Reason", dto.Reason);
        
        var idAppointment = await command.ExecuteScalarAsync() ??
            throw new AppointmentException(StatusCodes.Status500InternalServerError, "error");
        var newAppointment = await GetAppointmentByIdAsync(Convert.ToInt32(idAppointment));
        return newAppointment ??
               throw new AppointmentException(StatusCodes.Status500InternalServerError, "error");
    }

    public async Task<AppointmentDetailsDto> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto dto)
    {
        if (dto.AppointmentDate < DateTime.UtcNow)
        {
            throw new AppointmentException(StatusCodes.Status400BadRequest, "date must not be in the past");
        }
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        var existing = await GetAppointmentByIdAsync(idAppointment);
        if (existing == null)
        {
            throw new AppointmentException(StatusCodes.Status404NotFound, "error");
        }

        if (existing.Status == "Completed" && existing.AppointmentDate != dto.AppointmentDate)
        {
            throw new AppointmentException(StatusCodes.Status400BadRequest, "date is unchangeble");
        }
        await ValidatePatient(connection, dto.IdPatient);
        await ValidateDoctor(connection, dto.IdDoctor);
        if (dto.Status == "Scheduled")
        {
            await ValidateDate(connection, dto.IdDoctor, dto.AppointmentDate, idAppointment);
        }
        
        var sql = """
                  UPDATE dbo.Appointments
                  SET IdPatient = @IdPatient,
                      IdDoctor = @IdDoctor,
                      AppointmentDate = @AppointmentDate,
                      Status = @Status,
                      Reason = @Reason,
                      InternalNotes = @InternalNotes
                  WHERE IdAppointment = @IdAppointment;
                  """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@IdAppointment", idAppointment);
        command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
        command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
        command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
        command.Parameters.AddWithValue("@Status", dto.Status);
        command.Parameters.AddWithValue("@Reason", dto.Reason);
        command.Parameters.AddWithValue("@InternalNotes", string.IsNullOrWhiteSpace(dto.InternalNotes) ? DBNull.Value : dto.InternalNotes);
        
        await command.ExecuteNonQueryAsync();
        var updatedAppointment = await GetAppointmentByIdAsync(idAppointment);
        return updatedAppointment ??
               throw new AppointmentException(StatusCodes.Status500InternalServerError, "error");
    }

    public async Task DeleteAppointmentAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var existing = await GetAppointmentByIdAsync(idAppointment);
        if (existing == null)
        {
            throw new AppointmentException(StatusCodes.Status404NotFound, "error");
        }
        

        if (existing.Status == "Completed")
        {
            throw new AppointmentException(StatusCodes.Status409Conflict, "impossible to delete existing appointment");
        }
        
        var sql = "DELETE FROM dbo.Appointments WHERE IdAppointment = @IdAppointment;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@IdAppointment", idAppointment);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ValidatePatient(SqlConnection connection, int idPatient)
    {
        var query = "SELECT IsActive FROM Patients WHERE IdPatient = @IdPatient;";
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@IdPatient", idPatient);
        var value = await command.ExecuteScalarAsync();
        if (value == null)
        {
            throw new AppointmentException(StatusCodes.Status400BadRequest, "no patient found");
        }
        if (!Convert.ToBoolean(value)) {
            throw new AppointmentException(StatusCodes.Status404NotFound, "patient is inactive");
        }
    }

    private async Task ValidateDoctor(SqlConnection connection, int idDoctor)
    {
        var query = "SELECT IsActive FROM dbo.Doctors WHERE IdDoctor = @IdDoctor;";
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@IdDoctor", idDoctor);
        var value = await command.ExecuteScalarAsync();
        if (value == null)
        {
            throw new AppointmentException(StatusCodes.Status400BadRequest, "no doctor found");
        }
        if (!Convert.ToBoolean(value)) {
            throw new AppointmentException(StatusCodes.Status404NotFound, "doctor is inactive");
        }
    }

    private async Task ValidateDate(SqlConnection connection, int idDoctor, DateTime date, int? idAppointment)
    {
        var query = """
                    SELECT COUNT(1)
                    FROM dbo.Appointments
                    WHERE IdDoctor = @IdDoctor
                      AND AppointmentDate = @AppointmentDate
                      AND Status = N'Scheduled'
                      AND (@IdAppointment IS NULL OR IdAppointment <> @IdAppointment)
                    """;
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@IdDoctor", idDoctor);
        command.Parameters.AddWithValue("@AppointmentDate", date);
        command.Parameters.AddWithValue("@IdAppointment", idAppointment.HasValue ? idAppointment.Value : DBNull.Value);
        var value = await command.ExecuteScalarAsync();
        if (Convert.ToInt32(value) > 0)
        {
            throw new AppointmentException(StatusCodes.Status409Conflict, "doctor is unavailable");
        }
    }
}