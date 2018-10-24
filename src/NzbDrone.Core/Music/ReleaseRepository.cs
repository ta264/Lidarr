using System.Linq;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using Marr.Data;
using NLog;

namespace NzbDrone.Core.Music
{
    public interface IReleaseRepository : IBasicRepository<Release>
    {
        List<Release> FindByReleaseGroup(int id);
    }

    public class ReleaseRepository : BasicRepository<Release>, IReleaseRepository
    {
        public ReleaseRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<Release> FindByReleaseGroup(int id)
        {
            return Query.Where(r => r.ReleaseGroupId == id).ToList();
        }

    }
}
