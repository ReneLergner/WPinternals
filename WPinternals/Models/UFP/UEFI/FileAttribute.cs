using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnifiedFlashingPlatform.UEFI
{
    [Flags]
    public enum FileAttribute : ulong
    {
        EfiFileReadOnly = 1UL,
        EfiFileHidden = 2UL,
        EfiFileSystem = 4UL,
        EfiFileReserved = 8UL,
        EfiFileDirectory = 16UL,
        EfiFileArchive = 32UL,
        EfiFileValidAttr = 55UL
    }
}
