using MediaBrowser.Model.Serialization;
using System;
using System.IO;
using Newtonsoft.Json;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Class NewtonsoftJsonSerializer
    /// </summary>
    public class NewtonsoftJsonSerializer : IJsonSerializer
    {
        private readonly JsonSerializer _serializer = JsonSerializer.Create(new JsonSerializerSettings());

        /// <summary>
        /// Serializes to stream.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <param name="stream">The stream.</param>
        /// <exception cref="System.ArgumentNullException">obj</exception>
        public void SerializeToStream(object obj, Stream stream)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("obj");
            }

            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            using (var writer = new StreamWriter(stream))
            {
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    _serializer.Serialize(jsonWriter, obj);
                }
            }
        }

        /// <summary>
        /// Deserializes from stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="type">The type.</param>
        /// <returns>System.Object.</returns>
        public object DeserializeFromStream(Stream stream, Type type)
        {
            using (var streamReader = new StreamReader(stream))
            {
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    return _serializer.Deserialize(jsonReader, type);
                }
            }
        }

        /// <summary>
        /// Deserializes from stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream">The stream.</param>
        /// <returns>``0.</returns>
        public T DeserializeFromStream<T>(Stream stream)
        {
            return (T)DeserializeFromStream(stream, typeof(T));
        }

        /// <summary>
        /// Deserializes from string.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="text">The text.</param>
        /// <returns>``0.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public T DeserializeFromString<T>(string text)
        {
            return JsonConvert.DeserializeObject<T>(text);
        }

        /// <summary>
        /// Deserializes from string.
        /// </summary>
        /// <param name="json">The json.</param>
        /// <param name="type">The type.</param>
        /// <returns>System.Object.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public object DeserializeFromString(string json, Type type)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Serializes to string.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <returns>System.String.</returns>
        public string SerializeToString(object obj)
        {
            using (var streamWriter = new StringWriter())
            {
                using (var jsonWriter = new JsonTextWriter((streamWriter)))
                {
                    _serializer.Serialize(jsonWriter, obj);
                }
                return streamWriter.ToString();
            }
        }

        /// <summary>
        /// Serializes to bytes.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <returns>System.Byte[][].</returns>
        /// <exception cref="System.ArgumentNullException">obj</exception>
        public byte[] SerializeToBytes(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("obj");
            }

            using (var stream = new MemoryStream())
            {
                SerializeToStream(obj, stream);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Serializes to file.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <param name="file">The file.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <exception cref="System.ArgumentNullException">obj</exception>
        public void SerializeToFile(object obj, string file)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deserializes from file.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="file">The file.</param>
        /// <returns>System.Object.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public object DeserializeFromFile(Type type, string file)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deserializes from file.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="file">The file.</param>
        /// <returns>``0.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public T DeserializeFromFile<T>(string file) where T : class
        {
            throw new NotImplementedException();
        }
    }
}
