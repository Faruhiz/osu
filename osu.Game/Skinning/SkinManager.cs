﻿// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System.Linq;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.IO;
using osu.Game.Database;

namespace osu.Game.Skinning
{
    public class SkinManager : ArchiveModelImportManager<SkinInfo, SkinFileInfo>
    {
        public Bindable<Skin> CurrentSkin = new Bindable<Skin>(new DefaultSkin());

        public override string[] HandledExtensions => new[] { ".osk" };

        protected override SkinInfo CreateModel(ArchiveReader archive) => new SkinInfo
        {
            Name = archive.Filenames.First()
        };

        private SkinStore store;

        public SkinManager(Storage storage, DatabaseContextFactory contextFactory, IIpcHost importHost)
            : base(storage, contextFactory, new SkinStore(contextFactory, storage), importHost)
        {
        }
    }
}
