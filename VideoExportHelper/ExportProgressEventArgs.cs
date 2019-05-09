using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoExportHelper
{
    public class ExportProgressEventArgs : EventArgs
    {
        public int Progress { get; set; }

        public ExportProgressEventArgs(int progress)
        {
            Progress = progress;
        }
    }
}
