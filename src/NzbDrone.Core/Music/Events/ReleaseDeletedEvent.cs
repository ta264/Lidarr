using NzbDrone.Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NzbDrone.Core.Music.Events
{
    public class ReleaseDeletedEvent : IEvent
    {
        public Release Release { get; private set; }

        public ReleaseDeletedEvent(Release release)
        {
            Release = release;
        }
    }
}
