﻿using PCLStorage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace adeven.AdjustIo.PCL
{
    internal static class Util
    {
        //internal const string BaseUrl = "https://app.adjust.io";
        internal const string BaseUrl = "https://stage.adjust.io"; // TODO remove

        internal static string GetStringEncodedParameters(Dictionary<string, string> parameters)
        {
            if (parameters.Count == 0) return "";
            var firstPair = parameters.First();

            var stringBuilder = new StringBuilder(EncodedQueryParameter(firstPair, isFirstParameter: true));

            foreach (var pair in parameters.Skip(1))// skips the first "&"
            {
                stringBuilder.Append(EncodedQueryParameter(pair));
            }

            return stringBuilder.ToString();
        }

        internal static void DeleteFile(string filename)
        {
            try
            {
                var localStorage = FileSystem.Current.LocalStorage;
                var activityStateFile = localStorage.GetFileAsync(filename).Result;
                if (activityStateFile != null)
                {
                    activityStateFile.DeleteAsync().Wait();
                    Logger.Debug("File {0} deleted", filename);
                }
                else
                {
                    Logger.Debug("File {0} doesn't exist to delete", filename);
                }
            }
            catch (PCLStorage.Exceptions.FileNotFoundException)
            {
                Logger.Debug("File {0} doesn't exist to delete", filename);
            }
            catch (Exception)
            {
                Logger.Error("Error deleting {0} file", filename);
            }
        }

        internal static bool IsFileNotFound(this Exception ex)
        {
            // check if the exception type is File Not Found (FNF)
            if (ex is PCLStorage.Exceptions.FileNotFoundException
                || ex is FileNotFoundException)
                return true;

            // if the exception is an aggregate of exceptions
            // we'll check each of them recursively if they are FNF
            var agg = ex as AggregateException;
            if (agg != null)
            {
                foreach (var innerEx in agg.InnerExceptions)
                {
                    if (innerEx.IsFileNotFound())
                        return true;
                }
            }

            // if the exception it's not FNF and doesn't have an inner exception
            // then it's not a FNF
            if (ex.InnerException == null)
                return false;

            // check recursively if the inner exception is FNF
            if (ex.InnerException.IsFileNotFound())
                return true;

            // if all checks fails, the exception must not be a FNF
            return false;
        }

        internal static async Task<T> DeserializeFromFileAsync<T>(string fileName, Func<Stream, T> ObjectReader, Func<T> defaultReturn)
            where T : class
        {
            try
            {
                var localStorage = FileSystem.Current.LocalStorage;
                var activityStateFile = await localStorage.GetFileAsync(fileName);

                if (activityStateFile == null)
                {
                    throw new PCLStorage.Exceptions.FileNotFoundException(fileName);
                }

                T output;
                using (var stream = await activityStateFile.OpenAsync(FileAccess.Read))
                {
                    output = ObjectReader(stream);
                }
                Logger.Debug("Read from file {0}", fileName);

                // successful read
                return output;
            }
            catch (Exception ex)
            {
                if (ex.IsFileNotFound())
                    Logger.Error("Failed to read file {0} (not found)", fileName);
                else
                    Logger.Error("Failed to read file {0} ({1})", fileName, ex.Message);
            }
            // fresh start
            return defaultReturn();
        }

        internal static async Task SerializeToFileAsync<T>(string fileName, Action<Stream, T> ObjectWriter, T input)
            where T : class
        {
            try
            {
                var localStorage = FileSystem.Current.LocalStorage;
                var newActivityStateFile = await localStorage.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                using (var stream = await newActivityStateFile.OpenAsync(FileAccess.ReadAndWrite))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    ObjectWriter(stream, input);
                }
                Logger.Debug("Wrote to file {0}", fileName);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to write to file {0} ({1})", fileName, ex.Message);
            }
        }

        private static string EncodedQueryParameter(KeyValuePair<string, string> pair, bool isFirstParameter = false)
        {
            if (isFirstParameter)
                return String.Format("{0}={1}", Uri.EscapeDataString(pair.Key), Uri.EscapeDataString(pair.Value));
            else
                return String.Format("&{0}={1}", Uri.EscapeDataString(pair.Key), Uri.EscapeDataString(pair.Value));
        }

        internal static double SecondsFormat(this DateTime? date)
        {
            if (date == null)
                return -1;

            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            TimeSpan diff = date.Value.ToUniversalTime() - origin;
            return diff.TotalSeconds;
        }

        internal static double SecondsFormat(this TimeSpan? timeSpan)
        {
            if (timeSpan == null)
                return -1;
            else
                return timeSpan.Value.TotalSeconds;
        }

        #region Serialization

        internal static Int64 SerializeTimeSpanToLong(TimeSpan? timeSpan)
        {
            if (timeSpan.HasValue)
                return timeSpan.Value.Ticks;
            else
                return -1;
        }

        internal static Int64 SerializeDatetimeToLong(DateTime? dateTime)
        {
            if (dateTime.HasValue)
                return dateTime.Value.Ticks;
            else
                return -1;
        }

        internal static TimeSpan? DeserializeTimeSpanFromLong(Int64 ticks)
        {
            if (ticks == -1)
                return null;
            else
                return new TimeSpan(ticks);
        }

        internal static DateTime? DeserializeDateTimeFromLong(Int64 ticks)
        {
            if (ticks == -1)
                return null;
            else
                return new DateTime(ticks);
        }

        #endregion Serialization
    }
}