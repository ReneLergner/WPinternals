using System;

namespace WPinternalsSDK
{
    internal struct QUOTA_LIMITS
    {
        private readonly uint PagedPoolLimit;

        private readonly uint NonPagedPoolLimit;

        private readonly uint MinimumWorkingSetSize;

        private readonly uint MaximumWorkingSetSize;

        private readonly uint PagefileLimit;

        private readonly long TimeLimit;
    }
}
