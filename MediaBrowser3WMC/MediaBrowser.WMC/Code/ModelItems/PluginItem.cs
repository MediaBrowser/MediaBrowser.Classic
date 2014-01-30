﻿using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser.Model.Updates;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Code.ModelItems;
using MediaBrowser.Library.Entities;

namespace MediaBrowser.Library
{
    public class PluginItem : BaseModelItem
    {
        public PackageInfo Info { get; set; }

        /// <summary>
        /// The internal id of this package.
        /// </summary>
        /// <value>The id.</value>
        public int Id { get { return Info.id; } }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get { return Info.name; } }

        /// <summary>
        /// Gets or sets the short description.
        /// </summary>
        /// <value>The short description.</value>
        public string ShortDescription { get { return Info.shortDescription; } }

        /// <summary>
        /// Gets or sets the overview.
        /// </summary>
        /// <value>The overview.</value>
        public string Overview { get { return Info.overview; } }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is premium.
        /// </summary>
        /// <value><c>true</c> if this instance is premium; otherwise, <c>false</c>.</value>
        public bool IsPremium { get { return Info.isPremium; }}

        /// <summary>
        /// Gets or sets the thumb image.
        /// </summary>
        /// <value>The thumb image.</value>
        public string ThumbImage { get { return Info.thumbImage; } }

        /// <summary>
        /// Gets or sets the preview image.
        /// </summary>
        /// <value>The preview image.</value>
        public string PreviewImage { get { return Info.previewImage; } }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        public PackageType Type { get { return Info.type; } }

        /// <summary>
        /// Gets or sets the target filename.
        /// </summary>
        /// <value>The target filename.</value>
        public string TargetFilename { get { return Info.targetFilename; } }

        /// <summary>
        /// Gets or sets the owner.
        /// </summary>
        /// <value>The owner.</value>
        public string Owner { get { return Info.owner; } }

        /// <summary>
        /// Gets or sets the category.
        /// </summary>
        /// <value>The category.</value>
        public string Category { get { return Info.category; } }

        /// <summary>
        /// Gets or sets the feature id of this package (if premium).
        /// </summary>
        /// <value>The feature id.</value>
        public string FeatureId { get { return Info.featureId; } }

        /// <summary>
        /// Gets or sets the registration info for this package (if premium).
        /// </summary>
        /// <value>The registration info.</value>
        public string RegInfo { get { return Info.regInfo; } }

        /// <summary>
        /// Gets or sets the price for this package (if premium).
        /// </summary>
        /// <value>The price.</value>
        public float Price { get { return Info.price; } }

        /// <summary>
        /// Gets or sets the target system for this plug-in (Server, MBTheater, MBClassic).
        /// </summary>
        /// <value>The target system.</value>
        public PackageTargetSystem TargetSystem { get { return Info.targetSystem; } }

        /// <summary>
        /// The guid of the assembly associated with this package (if a plug-in).
        /// This is used to identify the proper item for automatic updates.
        /// </summary>
        /// <value>The name.</value>
        public string GuidString { get { return Info.guid; } }

        /// <summary>
        /// Gets or sets the total number of ratings for this package.
        /// </summary>
        /// <value>The total ratings.</value>
        public int TotalRatings { get { return Info.totalRatings; } }

        /// <summary>
        /// Gets or sets the average rating for this package .
        /// </summary>
        /// <value>The rating.</value>
        public float AvgRating { get { return Info.avgRating; } }

        /// <summary>
        /// Gets or sets whether or not this package is registered.
        /// </summary>
        /// <value>True if registered.</value>
        public bool IsRegistered { get { return IsPremium && Info.isRegistered; } }

        /// <summary>
        /// Gets or sets the expiration date for this package.
        /// </summary>
        /// <value>Expiration Date.</value>
        public DateTime ExpDate { get { return Info.expDate; } }

        public bool IsInTrial { get { return IsPremium && !IsRegistered && ExpDate > DateTime.Today; } }
        public int TrialDaysLeft { get { return (int)(ExpDate - DateTime.Today).TotalDays; } }
        public bool IsExpired { get { return IsPremium && !IsRegistered && ExpDate <= DateTime.Today; } }
        public bool IsFree { get { return !IsPremium; } }
        

        public string InstalledVersion { get; set; }
        public string InstalledVersionClass { get; set; }

        public string InstalledVersionDisplay { get { return !string.IsNullOrEmpty(InstalledVersion) ? "Version " + InstalledVersion + InstalledVersionClass : ""; } }

        /// <summary>
        /// Gets or sets the versions.
        /// </summary>
        /// <value>The versions.</value>
        public List<PackageVersionInfo> Versions { get { return Info.versions; } }

        // to keep mcml happy
        public PluginItem() {
        }

        public PluginItem(PackageInfo info)
        {
            Info = info;
            FireAllPropertiesChanged();
        }

    }
}
