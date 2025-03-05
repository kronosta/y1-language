using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kronosta.Language.Y1
{
    // This is a utility class used for "ref" parameters in lambdas
    public class FauxRefParameter<T>
    {
        public T Value;

        public FauxRefParameter(T value) { this.Value = value; }
    }

}
