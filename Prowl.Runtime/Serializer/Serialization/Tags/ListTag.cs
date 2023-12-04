﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace Prowl.Runtime.Serialization
{
    public class ListTag : Tag
    {
        public List<Tag> Tags { get; protected set; }
        public TagType Type { get; protected set; }
        public TagType ListType { get; protected set; }

        [JsonIgnore]
        public int Count => Tags.Count;

        public Tag this[int tagIdx]
        {
            get { return Get<Tag>(tagIdx); }
            set { Tags[tagIdx] = value; }
        }

        public ListTag() : this(new Tag[] { }, TagType.Null) { }
        public ListTag(IEnumerable<Tag> tags) : this(tags, tags.First().GetTagType()) { }
        public ListTag(TagType listType = TagType.Null) : this(new Tag[] { }, listType) { }
        public ListTag(IEnumerable<Tag> tags, TagType listType)
        {
            Tags = new List<Tag>();
            ListType = listType;

            if (tags != null) Tags.AddRange(tags);
        }

        public static explicit operator List<Tag>(ListTag tag) => tag.Tags;

        public Tag Get(int tagIdx) => Get<Tag>(tagIdx);
        public T Get<T>(int tagIdx) where T : Tag => (T)Tags[tagIdx];

        public void Add(Tag tag) => Tags.Add(tag);

        public void SetListType(TagType listType)
        {
            foreach (var tag in Tags)
                if (tag.GetTagType() != listType)
                    throw new Exception("All list items must be the specified tag type.");
            ListType = listType;
        }

        public override TagType GetTagType() => TagType.List;

        public override Tag Clone()
        {
            var tags = new List<Tag>();
            foreach (var tag in Tags) tags.Add(tag.Clone());
            return new ListTag(tags, ListType);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("ListTAG");
            sb.AppendFormat(": {0} entries\n", Tags.Count);

            sb.Append("{\n");
            foreach (Tag tag in Tags) sb.AppendFormat("\t{0}\n", tag.ToString().Replace("\n", "\n\t"));
            sb.Append("}");
            return sb.ToString();
        }
    }
}