using Domain.Infrastructure.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loader.Infrastructure.Interface
{
    public interface ILoggerFactoryBase
    {
        public ILoggerBase CreateLogger<T>() where T : class;

    }
}
