using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.DragDropUtils
{
    public interface IDragItem
    {
        bool CanDrag { get; }
    }
}
