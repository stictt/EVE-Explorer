using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Infrastructure.Interface
{
    public interface ILoggerBase
    {
        public void LogInformation(string message);
        public void LogError(string message);
        public void LogWarning(string message);
    }
}
