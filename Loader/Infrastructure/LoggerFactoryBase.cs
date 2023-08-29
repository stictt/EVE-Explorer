using Domain.Infrastructure.Interface;
using Loader.Infrastructure.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loader.Infrastructure
{
    public class LoggerFactoryBase : ILoggerFactoryBase
    {
        private Action<string> _logger;
        public LoggerFactoryBase(Action<string> logger)
        {
            _logger = logger;
        }
        public LoggerFactoryBase()
        {

        }
        public ILoggerBase CreateLogger<T>() where T : class
        {
            return new LoggerBase(typeof(T).Name, _logger);
        }
    }
}
