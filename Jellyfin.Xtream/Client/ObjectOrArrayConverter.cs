// Copyright (C) 2022  Kevin Jilissen

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Xtream.Client;

/// <summary>
/// Converts JSON that can be either an object or an array into a List.
/// Some Xtream providers return objects instead of arrays for empty or single-item lists.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
public class ObjectOrArrayConverter<T> : JsonConverter
{
    /// <inheritdoc/>
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(List<T>);
    }

    /// <inheritdoc/>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);

        if (token.Type == JTokenType.Array)
        {
            // Standard case: it's already an array
            return token.ToObject<List<T>>();
        }
        else if (token.Type == JTokenType.Object)
        {
            // Non-standard case: it's an object, convert values to list
            var list = new List<T>();
            foreach (var property in ((JObject)token).Properties())
            {
                var item = property.Value.ToObject<T>();
                if (item != null)
                {
                    list.Add(item);
                }
            }

            return list;
        }

        // Return empty list for null or other types
        return new List<T>();
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
}
