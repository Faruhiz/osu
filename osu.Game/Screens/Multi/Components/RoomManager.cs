// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;
using osu.Game.Rulesets;

namespace osu.Game.Screens.Multi.Components
{
    public abstract class RoomManager : CompositeDrawable, IRoomManager
    {
        public event Action RoomsUpdated;

        private readonly BindableList<Room> rooms = new BindableList<Room>();

        public Bindable<bool> InitialRoomsReceived { get; } = new Bindable<bool>();

        public IBindableList<Room> Rooms => rooms;

        [Resolved]
        private RulesetStore rulesets { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; }

        private Room joinedRoom;

        protected RoomManager()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = CreatePollingComponents().Select(p =>
            {
                p.InitialRoomsReceived.BindTo(InitialRoomsReceived);
                p.RoomsReceived = onRoomsReceived;
                return p;
            }).ToList();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            PartRoom();
        }

        public virtual void CreateRoom(Room room, Action<Room> onSuccess = null, Action<string> onError = null)
        {
            room.Host.Value = api.LocalUser.Value;

            var req = new CreateRoomRequest(room);

            req.Success += result =>
            {
                joinedRoom = room;

                update(room, result);
                addRoom(room);

                RoomsUpdated?.Invoke();
                onSuccess?.Invoke(room);
            };

            req.Failure += exception =>
            {
                if (req.Result != null)
                    onError?.Invoke(req.Result.Error);
                else
                    Logger.Log($"Failed to create the room: {exception}", level: LogLevel.Important);
            };

            api.Queue(req);
        }

        private JoinRoomRequest currentJoinRoomRequest;

        public virtual void JoinRoom(Room room, Action<Room> onSuccess = null, Action<string> onError = null)
        {
            currentJoinRoomRequest?.Cancel();
            currentJoinRoomRequest = new JoinRoomRequest(room);

            currentJoinRoomRequest.Success += () =>
            {
                joinedRoom = room;
                onSuccess?.Invoke(room);
            };

            currentJoinRoomRequest.Failure += exception =>
            {
                if (!(exception is OperationCanceledException))
                    Logger.Log($"Failed to join room: {exception}", level: LogLevel.Important);
                onError?.Invoke(exception.ToString());
            };

            api.Queue(currentJoinRoomRequest);
        }

        public void PartRoom()
        {
            currentJoinRoomRequest?.Cancel();

            if (joinedRoom == null)
                return;

            api.Queue(new PartRoomRequest(joinedRoom));
            joinedRoom = null;
        }

        private readonly HashSet<int> ignoredRooms = new HashSet<int>();

        private void onRoomsReceived(List<Room> received)
        {
            // Remove past matches
            foreach (var r in rooms.ToList())
            {
                if (received.All(e => e.RoomID.Value != r.RoomID.Value))
                    rooms.Remove(r);
            }

            for (int i = 0; i < received.Count; i++)
            {
                var room = received[i];

                Debug.Assert(room.RoomID.Value != null);

                if (ignoredRooms.Contains(room.RoomID.Value.Value))
                    continue;

                room.Position.Value = i;

                try
                {
                    update(room, room);
                    addRoom(room);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to update room: {room.Name.Value}.");

                    ignoredRooms.Add(room.RoomID.Value.Value);
                    rooms.Remove(room);
                }
            }

            RoomsUpdated?.Invoke();
        }

        /// <summary>
        /// Updates a local <see cref="Room"/> with a remote copy.
        /// </summary>
        /// <param name="local">The local <see cref="Room"/> to update.</param>
        /// <param name="remote">The remote <see cref="Room"/> to update with.</param>
        private void update(Room local, Room remote)
        {
            foreach (var pi in remote.Playlist)
                pi.MapObjects(beatmaps, rulesets);

            local.CopyFrom(remote);
        }

        /// <summary>
        /// Adds a <see cref="Room"/> to the list of available rooms.
        /// </summary>
        /// <param name="room">The <see cref="Room"/> to add.</param>
        private void addRoom(Room room)
        {
            var existing = rooms.FirstOrDefault(e => e.RoomID.Value == room.RoomID.Value);
            if (existing == null)
                rooms.Add(room);
            else
                existing.CopyFrom(room);
        }

        protected abstract RoomPollingComponent[] CreatePollingComponents();
    }
}
