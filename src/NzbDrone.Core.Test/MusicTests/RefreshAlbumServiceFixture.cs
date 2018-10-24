using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Commands;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class RefreshAlbumServiceFixture : CoreTest<RefreshAlbumService>
    {
        private Artist _artist;
        private List<Album> _albums;
        private List<Release> _releases;
        private string _fakeArtistForeignId = "xxx-xxx-xxx";
        private List<ArtistMetadata> _fakeArtists = new List<ArtistMetadata> { new ArtistMetadata() };

        [SetUp]
        public void Setup()
        {

            var release = Builder<Release>
                .CreateNew()
                .With(s => s.Media = new List<Medium> { new Medium { Number = 1 } })
                .With(s => s.ForeignReleaseId = "xxx-xxx-xxx-xxx")
                .Build();

            _releases = new List<Release> { release };
            
            var album1 = Builder<Album>.CreateNew()
                .With(s => s.ForeignReleaseGroupId = "1")
                .With(s => s.SelectedRelease = release)
                .With(s => s.Releases = _releases)
                .Build();

            _albums = new List<Album>{ album1 };

            _artist = Builder<Artist>.CreateNew()
                .With(s => s.ReleaseGroups = _albums)
                .Build();



            Mocker.GetMock<IArtistService>()
                  .Setup(s => s.GetArtist(_artist.Id))
                  .Returns(_artist);

            Mocker.GetMock<IReleaseService>()
                .Setup(s => s.GetReleasesByReleaseGroup(album1.Id))
                .Returns(_releases);
            Mocker.GetMock<IReleaseService>()
                .Setup(s => s.GetRelease(release.Id))
                .Returns(release);
            
            Mocker.GetMock<IProvideAlbumInfo>()
                .Setup(s => s.GetAlbumInfo(It.IsAny<string>()))
                  .Callback(() => { throw new AlbumNotFoundException(album1.ForeignReleaseGroupId); });

            Mocker.GetMock<ICheckIfAlbumShouldBeRefreshed>()
                .Setup(s => s.ShouldRefresh(It.IsAny<Album>()))
                .Returns(true);
        }

        private void GivenNewAlbumInfo(Album album)
        {
            Mocker.GetMock<IProvideAlbumInfo>()
                .Setup(s => s.GetAlbumInfo(_albums.First().ForeignReleaseGroupId))
                .Returns(new Tuple<string, Album, List<ArtistMetadata>>(_fakeArtistForeignId, album, _fakeArtists));
        }

        [Test]
        public void should_log_error_if_musicbrainz_id_not_found()
        {
            Subject.RefreshAlbumInfo(_albums, false);

            Mocker.GetMock<IAlbumService>()
                .Verify(v => v.UpdateMany(It.IsAny<List<Album>>()), Times.Never());

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_update_if_musicbrainz_id_changed()
        {
            var newAlbumInfo = _albums.FirstOrDefault().JsonClone();
            newAlbumInfo.ForeignReleaseGroupId = _albums.First().ForeignReleaseGroupId + 1;
            newAlbumInfo.Releases = _releases;

            GivenNewAlbumInfo(newAlbumInfo);

            Subject.RefreshAlbumInfo(_albums, false);

            Mocker.GetMock<IAlbumService>()
                .Verify(v => v.UpdateMany(It.Is<List<Album>>(s => s.First().ForeignReleaseGroupId == newAlbumInfo.ForeignReleaseGroupId)));

            ExceptionVerification.ExpectedWarns(1);
        }
    }
}
