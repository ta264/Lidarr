using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Common.Serializer;
using System.Collections.Generic;
using NzbDrone.Core.Music;
using System.Data;
using System;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(023)]
    public class add_release_groups_etc : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // ARTISTS TABLE
            
            Create.TableForModel("ArtistMetadata")
                .WithColumn("ForeignArtistId").AsString().Unique()
                .WithColumn("Name").AsString()
                .WithColumn("Overview").AsString().Nullable()
                .WithColumn("Disambiguation").AsString().Nullable()
                .WithColumn("Type").AsString().Nullable()
                .WithColumn("Status").AsInt32()
                .WithColumn("Images").AsString()
                .WithColumn("Links").AsString().Nullable()
                .WithColumn("Genres").AsString().Nullable()
                .WithColumn("Ratings").AsString().Nullable()
                .WithColumn("Members").AsString().Nullable();

            // we want to preserve the artist ID.  Shove all the metadata into the metadata table.
            Execute.Sql(@"INSERT INTO ArtistMetadata (ForeignArtistId, Name, Overview, Disambiguation, Type, Status, Images, Links, Genres, Ratings, Members)
                          SELECT ForeignArtistId, Name, Overview, Disambiguation, ArtistType, Status, Images, Links, Genres, Ratings, Members
                          FROM Artists");
            
            // Add an ArtistMetadataId column to Artists
            Alter.Table("Artists").AddColumn("ArtistMetadataId").AsInt32().WithDefaultValue(0);

            // Update artistmetadataId
            Execute.Sql(@"UPDATE Artists
                          SET ArtistMetadataId = (SELECT ArtistMetadata.Id 
                                                  FROM ArtistMetadata 
                                                  WHERE ArtistMetadata.ForeignArtistId = Artists.ForeignArtistId)");

            // RELEASES TABLE - Do this before we mess with the Albums table

            Create.TableForModel("Release")
                .WithColumn("ForeignReleaseId").AsString().Unique()
                .WithColumn("ReleaseGroupId").AsInt32().Indexed()
                .WithColumn("Title").AsString()
                .WithColumn("Status").AsString()
                .WithColumn("Duration").AsInt32().WithDefaultValue(0)
                .WithColumn("Label").AsString().Nullable()
                .WithColumn("Disambiguation").AsString().Nullable()
                .WithColumn("Country").AsString().Nullable()
                .WithColumn("Media").AsString().Nullable()
                .WithColumn("TrackCount").AsInt32().Nullable();

            Execute.WithConnection(PopulateReleases);

            // ALBUMS TABLE

            // Add in the extra columns and update artist metadata id
            Alter.Table("Albums").AddColumn("ArtistMetadataId").AsInt32().WithDefaultValue(0);
            Alter.Table("Albums").AddColumn("SelectedReleaseId").AsInt32().WithDefaultValue(0);
            Rename.Column("ForeignAlbumId").OnTable("Albums").To("ForeignReleaseGroupId");

            // Set metadata ID
            Execute.Sql(@"UPDATE Albums
                          SET ArtistMetadataId = (SELECT ArtistMetadata.Id 
                                                  FROM ArtistMetadata 
                                                  JOIN Artists ON ArtistMetadata.Id = Artists.ArtistMetadataId
                                                  WHERE Albums.ArtistId = Artists.Id)");
            // Set Selected Release (to the only release we've bothered populating)
            Execute.Sql(@"UPDATE Albums
                          SET SelectedReleaseId = (SELECT Release.Id 
                                                   FROM Release
                                                   WHERE Albums.Id = Release.ReleaseGroupId)");
            
            // TRACKS TABLE

            Alter.Table("Tracks").AddColumn("ReleaseId").AsInt32().WithDefaultValue(0);
            Alter.Table("Tracks").AddColumn("ArtistMetadataId").AsInt32().WithDefaultValue(0);
            
            // Set Selected Release (to the only release we've bothered populating)
            Execute.Sql(@"UPDATE Tracks
                          SET ReleaseId = (SELECT Release.Id 
                                           FROM Release
                                           JOIN Albums ON Release.ReleaseGroupId = Albums.Id
                                           WHERE Albums.Id = Tracks.AlbumId)");

            // CLEAR OUT OLD COLUMNS

            // Remove the columns in Artists now in ArtistMetadata
            Delete.Column("ForeignArtistId")
                .Column("Name")
                .Column("Overview")
                .Column("Disambiguation")
                .Column("ArtistType")
                .Column("Status")
                .Column("Images")
                .Column("Links")
                .Column("Genres")
                .Column("Ratings")
                .Column("Members")
                // as well as the ones no longer used
                .Column("MBId")
                .Column("AMId")
                .Column("TADBId")
                .Column("DiscogsId")
                .Column("NameSlug")
                .Column("LastDiskSync")
                .FromTable("Artists");

            // Remove old columns from Albums
            Delete.Column("ArtistId")
                .Column("MBId")
                .Column("AMId")
                .Column("TADBId")
                .Column("DiscogsId")
                .Column("TitleSlug")
                .Column("Label")
                .Column("SortTitle")
                .Column("Tags")
                .Column("Duration")
                .Column("Media")
                .Column("Releases")
                .Column("CurrentRelease")
                .Column("LastDiskSync")
                .FromTable("Albums");

            // Remove old columns from Tracks
            Delete.Column("ArtistId")
                .Column("AlbumId")
                .Column("Compilation")
                .Column("DiscNumber")
                .Column("Monitored")
                .FromTable("Tracks");

            // Remove old columns from TrackFiles
            Delete.Column("ArtistId").FromTable("TrackFiles");
            
            // Rename tables
            Rename.Table("Artists").To("Artist");
            Rename.Table("Albums").To("ReleaseGroup");
            Rename.Table("Tracks").To("Track");
            Rename.Table("TrackFiles").To("TrackFile");

            // Add indices
            Create.Index().OnTable("Artist").OnColumn("ArtistMetadataId").Ascending();
            Create.Index().OnTable("ReleaseGroup").OnColumn("ArtistMetadataId").Ascending();
            Create.Index().OnTable("ReleaseGroup").OnColumn("SelectedReleaseId").Ascending();
            Create.Index().OnTable("Track").OnColumn("ArtistMetadataId").Ascending();
            Create.Index().OnTable("Track").OnColumn("ReleaseId").Ascending();

            // Force a metadata refresh
            Update.Table("Artist").Set(new { LastInfoSync = new System.DateTime(2018, 1, 1, 0, 0, 1)}).AllRows();
            Update.Table("ReleaseGroup").Set(new { LastInfoSync = new System.DateTime(2018, 1, 1, 0, 0, 1)}).AllRows();
            Update.Table("ScheduledTasks")
                .Set(new { LastExecution = new System.DateTime(2018, 1, 1, 0, 0, 1)})
                .Where(new { TypeName = "NzbDrone.Core.Music.Commands.RefreshArtistCommand" });

        }

        private void PopulateReleases(IDbConnection conn, IDbTransaction tran)
        {
            var releases = ReadReleasesFromAlbums(conn, tran);
            WriteReleasesToReleases(releases,conn, tran);
        }

        private List<Release> ReadReleasesFromAlbums(IDbConnection conn, IDbTransaction tran)
        {

            // need to get all the old albums
            var releases = new List<Release>();

            using (var getReleasesCmd = conn.CreateCommand())
            {
                getReleasesCmd.Transaction = tran;
                getReleasesCmd.CommandText = @"SELECT Id, CurrentRelease FROM Albums";

                using (var releaseReader = getReleasesCmd.ExecuteReader())
                {
                    while (releaseReader.Read())
                    {
                        int rgId = releaseReader.GetInt32(0);
                        var albumRelease = Json.Deserialize<AlbumRelease>(releaseReader.GetString(1));
                        var media = new List<Medium>();
                        for (var i = 1; i <= albumRelease.MediaCount; i++)
                            media.Add(new Medium { Number = i, Name = "", Format = albumRelease.Format } );
                        
                        releases.Add(new Release {
                                ReleaseGroupId = rgId,
                                ForeignReleaseId = albumRelease.Id,
                                Title = albumRelease.Title,
                                Status = "",
                                Duration = 0,
                                Label = albumRelease.Label,
                                Disambiguation = albumRelease.Disambiguation,
                                Country = albumRelease.Country,
                                Media=media,
                                TrackCount = albumRelease.TrackCount
                            });
                    }
                }
            }

            return releases;
        }

        private void WriteReleasesToReleases(List<Release> releases, IDbConnection conn, IDbTransaction tran)
        {
            foreach (var release in releases)
            {
                using (var writeReleaseCmd = conn.CreateCommand())
                {
                    writeReleaseCmd.Transaction = tran;
                    writeReleaseCmd.CommandText =
                        "INSERT INTO Release (ReleaseGroupId, ForeignReleaseId, Title, Status, Duration, Label, Disambiguation, Country, Media, TrackCount) " +
                        "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
                    writeReleaseCmd.AddParameter(release.ReleaseGroupId);
                    writeReleaseCmd.AddParameter(release.ForeignReleaseId);
                    writeReleaseCmd.AddParameter(release.Title);
                    writeReleaseCmd.AddParameter(release.Status);
                    writeReleaseCmd.AddParameter(release.Duration);
                    writeReleaseCmd.AddParameter(release.Label.ToJson());
                    writeReleaseCmd.AddParameter(release.Disambiguation);
                    writeReleaseCmd.AddParameter(release.Country.ToJson());
                    writeReleaseCmd.AddParameter(release.Media.ToJson());
                    writeReleaseCmd.AddParameter(release.TrackCount);

                    writeReleaseCmd.ExecuteNonQuery();
                    Console.Write("Inserted release for " + release.ReleaseGroupId + "\n");
                }
            }
        }
    }
}
