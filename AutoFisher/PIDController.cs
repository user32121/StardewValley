using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoFisher
{
    internal class PIDController
    {
        public float P;
        public float I;
        public float D;

        private float integralError;
        private float prevError;
        private bool firstIteration;

        public PIDController(float p, float i, float d)
        {
            P = p;
            I = i;
            D = d;
            integralError = 0;
            firstIteration = true;
        }

        public float Update(float e)
        {
            integralError += e;

            float vP = P * e;
            float vI = I * integralError;
            float vD = firstIteration ? 0 : (D * (e - prevError));

            prevError = e;
            firstIteration = false;

            return vP + vI + vD;
        }

        public void Reset()
        {
            integralError = 0;
            firstIteration = true;
        }
    }
}
