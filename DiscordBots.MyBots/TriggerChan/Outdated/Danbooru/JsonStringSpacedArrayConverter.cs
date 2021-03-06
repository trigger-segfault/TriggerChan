﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TriggersTools.DiscordBots.TriggerChan.Danbooru {
	public class JsonStringSpacedArrayConverter : JsonConverter {

		public char Separator { get; set; }

		public JsonStringSpacedArrayConverter() {
			Separator = ' ';
		}
		
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
			string[] array = (string[]) value;
			writer.WriteValue(string.Join(new string(Separator, 1), array));
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
			string arrayStr = (string) reader.Value;
			return arrayStr.Split(Separator);
		}

		public override bool CanConvert(Type objectType) {
			return objectType == typeof(string);
		}
	}
}
