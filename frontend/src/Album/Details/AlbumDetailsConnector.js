/* eslint max-params: 0 */
import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { findCommand } from 'Utilities/Command';
import { registerPagePopulator, unregisterPagePopulator } from 'Utilities/pagePopulator';
import createCommandsSelector from 'Store/Selectors/createCommandsSelector';
import { fetchTracks, clearTracks } from 'Store/Actions/trackActions';
import { fetchTrackFiles, clearTrackFiles } from 'Store/Actions/trackFileActions';
import { executeCommand } from 'Store/Actions/commandActions';
import * as commandNames from 'Commands/commandNames';
import AlbumDetails from './AlbumDetails';
import createAllArtistSelector from 'Store/Selectors/createAllArtistSelector';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';

function createMapStateToProps() {
  return createSelector(
    (state, { foreignAlbumId }) => foreignAlbumId,
    (state) => state.tracks,
    (state) => state.trackFiles,
    (state) => state.albums,
    createAllArtistSelector(),
    createCommandsSelector(),
    createUISettingsSelector(),
    (foreignAlbumId, tracks, trackFiles, albums, artists, commands, uiSettings) => {
      const sortedAlbums = _.orderBy(albums.items, 'releaseDate');
      const albumIndex = _.findIndex(sortedAlbums, { foreignAlbumId });
      const album = sortedAlbums[albumIndex];
      const artist = _.find(artists, { id: album.artistId });

      if (!album) {
        return {};
      }

      const previousAlbum = sortedAlbums[albumIndex - 1] || _.last(sortedAlbums);
      const nextAlbum = sortedAlbums[albumIndex + 1] || _.first(sortedAlbums);
      const isSearching = !!findCommand(commands, { name: commandNames.ALBUM_SEARCH });

      const isFetching = tracks.isFetching || trackFiles.isFetching;
      const isPopulated = tracks.isPopulated && trackFiles.isPopulated;
      const tracksError = tracks.error;
      const trackFilesError = trackFiles.error;
      const currentReleaseId = album.currentRelease.id;

      return {
        ...album,
        shortDateFormat: uiSettings.shortDateFormat,
        artist,
        isSearching,
        isFetching,
        isPopulated,
        tracksError,
        trackFilesError,
        currentReleaseId,
        previousAlbum,
        nextAlbum
      };
    }
  );
}

const mapDispatchToProps = {
  executeCommand,
  fetchTracks,
  clearTracks,
  fetchTrackFiles,
  clearTrackFiles
};

class AlbumDetailsConnector extends Component {

  componentDidMount() {
    registerPagePopulator(this.populate);
    this.populate();
  }

  componentDidUpdate(prevProps) {
    const {
      currentReleaseId
    } = this.props;

    // If the id has changed we need to clear the tracks/track
    // files and fetch from the server.

    if (prevProps.currentReleaseId !== currentReleaseId) {
      this.unpopulate();
      this.populate();
    }
  }

  componentWillUnmount() {
    unregisterPagePopulator(this.populate);
    this.unpopulate();
  }

  //
  // Control

  populate = () => {
    const albumId = this.props.id;

    this.props.fetchTracks({ albumId });
    this.props.fetchTrackFiles({ albumId });
  }

  unpopulate = () => {
    this.props.clearTracks();
    this.props.clearTrackFiles();
  }

  //
  // Listeners

  onSearchPress = () => {
    this.props.executeCommand({
      name: commandNames.ALBUM_SEARCH,
      albumIds: [this.props.id]
    });
  }

  //
  // Render

  render() {
    return (
      <AlbumDetails
        {...this.props}
        onSearchPress={this.onSearchPress}
      />
    );
  }
}

AlbumDetailsConnector.propTypes = {
  id: PropTypes.number,
  currentReleaseId: PropTypes.string.isRequired,
  isAlbumFetching: PropTypes.bool,
  isAlbumPopulated: PropTypes.bool,
  foreignAlbumId: PropTypes.string.isRequired,
  fetchTracks: PropTypes.func.isRequired,
  clearTracks: PropTypes.func.isRequired,
  fetchTrackFiles: PropTypes.func.isRequired,
  clearTrackFiles: PropTypes.func.isRequired,
  executeCommand: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AlbumDetailsConnector);
