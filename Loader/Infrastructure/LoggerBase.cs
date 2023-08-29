using Domain.Infrastructure.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loader.Infrastructure
{
    public class LoggerBase : ILoggerBase
    {
        private string _source;
        Action<string> _message;
        public LoggerBase(string source, Action<string> message)
        {
            _source = source;
            _message = message;
        }

        public void LogError(string message)
        {
            _message?.Invoke(_source + " " + message);
        }

        public void LogInformation(string message)
        {
            _message?.Invoke(_source + " " + message);
        }

        public void LogWarning(string message)
        {
            _message?.Invoke(_source + " " + message);
        }
    }
}
