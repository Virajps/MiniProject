using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Repositories.Models;

namespace Repositories.Services
{
    public interface IRabbitRegistration
    {
        Task<IConnection> GetConnection();
         Task PublishUserRegistrationAsync(IConnection conn, t_Employee user);
         Task PublishAttendanceEventAsync(IConnection conn, int employeeId, string eventType, object payload);
    }
}