using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder
{
    public class EventArgs<T> : EventArgs
    {
        private T value;

        public EventArgs(T value)
        {
            this.value = value;
        }

        public T Value
        {
            get
            {
                return this.value;
            }
        }
    }
}
