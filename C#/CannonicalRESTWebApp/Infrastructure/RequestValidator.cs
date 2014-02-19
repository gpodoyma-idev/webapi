namespace CannonicalRESTWebApp.Infrastructure
{
    using System;
    using System.Net;
    using System.Net.Http;

    using CannonicalRESTWebApp.Models;

    using Microsoft.ApplicationServer.Http.Dispatcher;

    internal static class RequestValidator
    {
        #region Public Methods

        public static bool IsValidKey(int key)
        {
            return key >= 0;
        }

        public static void Validate(int key)
        {
            ValidateRequest(IsValidKey, key, "Invalid key");
        }

        public static void Validate(int key, Sample resource)
        {
            Validate(key);
            Validate(resource);
        }

        #endregion

        #region Methods

        internal static bool IsPositive(int number)
        {
            return number >= 0;
        }

        internal static bool IsValidKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key);
        }

        internal static bool IsValidResource(Sample sample)
        {
            return (sample != null && sample.IsValid());
        }

        internal static void IsValidSkip(int skip)
        {
            ValidateRequest(IsPositive, skip, "Invalid skip value {0}", skip);
        }

        internal static void IsValidTake(int take)
        {
            ValidateRequest(IsPositive, take, "Invalid take value {0}", take);
        }

        internal static void Validate(Sample sample)
        {
            ValidateRequest(IsValidResource, sample, "Invalid Sample Sample");
        }

        internal static void Validate(string key)
        {
            ValidateRequest(IsValidKey, key, "Invalid key");
        }

        internal static void Validate(string key, Sample resource)
        {
            Validate(key);
            Validate(resource);
        }

        internal static void ValidateRequest(Func<bool> isValid, string format, params object[] args)
        {
            if (!isValid())
            {
                throw new HttpResponseException(new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest, Content = new StringContent(string.Format(format, args)) });
            }
        }

        internal static void ValidateRequest<T1>(Func<T1, bool> isValid, T1 arg1, string format, params object[] args)
        {
            ValidateRequest(() => isValid(arg1), format, args);
        }

        #endregion
    }
}