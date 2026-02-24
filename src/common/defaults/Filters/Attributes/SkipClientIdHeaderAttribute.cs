using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EI.API.ServiceDefaults.Filters.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class SkipClientIdHeaderAttribute : Attribute
    {
    }
}
