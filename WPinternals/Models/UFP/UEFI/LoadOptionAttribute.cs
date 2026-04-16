using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnifiedFlashingPlatform.UEFI
{
    [Flags]
    public enum LoadOptionAttribute : uint
    {
        LoadOptionCategoryBoot = 0U,
        LoadOptionActive = 1U,
        LoadOptionForceReconnect = 2U,
        LoadOptionHidden = 8U,
        LoadOptionCategoryApp = 256U,
        LoadOptionCategory = 7936U
    }
}
