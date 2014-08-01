﻿using AdjustSdk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustSdk.Pcl
{
    public class RequestHandler : IRequestHandler
    {
        private static readonly TimeSpan Timeout = new TimeSpan(0, 1, 0);       // 1 minute

        private IPackageHandler PackageHandler;
        private Action<ResponseData> ResponseDelegate;
        private ILogger Logger;
        private HttpMessageHandler HttpMessageHandler;

        private struct SendResponse
        {
            internal ActivityPackage ActivtyPackage;
            internal ResponseData ResponseData;
            internal Dictionary<string, string> JsonDict;
        }

        public RequestHandler(IPackageHandler packageHandler)
        {
            PackageHandler = packageHandler;
            Logger = AdjustFactory.Logger;
            HttpMessageHandler = AdjustFactory.GetHttpMessageHandler();
        }

        public void SetResponseDelegate(Action<ResponseData> responseDelegate)
        {
            ResponseDelegate = responseDelegate;
        }

        public void SendPackage(ActivityPackage package)
        {
            Task.Factory.StartNew(() => SendInternal(package))
                // continuation used to prevent unhandled exceptions in SendInternal
                // not signaling the WaitHandle in PackageHandler and preventing deadlocks
                .ContinueWith((sendResponse) => PackageSent(sendResponse));
        }

        private SendResponse SendInternal(ActivityPackage activityPackage)
        {
            SendResponse sendResponse;
            try
            {
                using (var httpResponseMessage = ExecuteRequest(activityPackage))
                {
                    sendResponse = ProcessResponse(httpResponseMessage, activityPackage);
                }
            }
            catch (WebException we) { sendResponse = ProcessException(we, activityPackage); }
            catch (Exception ex) { sendResponse = ProcessException(ex, activityPackage); }

            sendResponse.ActivtyPackage = activityPackage;
            return sendResponse;
        }

        private HttpResponseMessage ExecuteRequest(ActivityPackage activityPackage)
        {
            var httpClient = new HttpClient(HttpMessageHandler);

            httpClient.Timeout = Timeout;
            httpClient.DefaultRequestHeaders.Add("Client-SDK", activityPackage.ClientSdk);
            httpClient.DefaultRequestHeaders.Add("User-Agent", activityPackage.UserAgent);

            var url = Util.BaseUrl + activityPackage.Path;

            var sNow = Util.DateFormat(DateTime.Now);
            activityPackage.Parameters["sent_at"] = sNow;

            using (var parameters = new FormUrlEncodedContent(activityPackage.Parameters))
            {
                return httpClient.PostAsync(url, parameters).Result;
            }
        }

        private SendResponse ProcessResponse(HttpResponseMessage httpResponseMessage, ActivityPackage activityPackage)
        {
            ResponseData responseData = new ResponseData();
            Dictionary<string, string> jsonDict = null;

            using (var content = httpResponseMessage.Content)
            {
                var responseString = content.ReadAsStringAsync().Result;
                jsonDict = Util.BuildJsonDict(responseString);

                responseData.SetResponseData(jsonDict, responseString);

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    responseData.Success = true;

                    Logger.Info("{0}", activityPackage.SuccessMessage());
                }
                else if (httpResponseMessage.StatusCode == HttpStatusCode.InternalServerError   // 500
                    || httpResponseMessage.StatusCode == HttpStatusCode.NotImplemented)         // 501
                {
                    Logger.Error("{0}. ({1}, {2}).",
                        activityPackage.FailureMessage(),
                        responseString.TrimEnd('\r', '\n'),
                        (int)httpResponseMessage.StatusCode);
                }
                else
                {
                    responseData.WillRetry = true;

                    Logger.Error("{0}. ({1}). Will retry later.",
                        activityPackage.FailureMessage(),
                        (int)httpResponseMessage.StatusCode);
                }
            }

            return new SendResponse
            {
                ResponseData = responseData,
                JsonDict = jsonDict
            };
        }

        private SendResponse ProcessException(WebException webException, ActivityPackage activityPackage)
        {
            ResponseData responseData = new ResponseData();
            Dictionary<string, string> jsonDict = null;


            using (var response = webException.Response as HttpWebResponse)
            using (var streamResponse = response.GetResponseStream())
            using (var streamReader = new StreamReader(streamResponse))
            {
                var responseString = streamReader.ReadToEnd();
                jsonDict = Util.BuildJsonDict(responseString);

                responseData.SetResponseData(jsonDict, responseString);
                responseData.WillRetry = true;

                Logger.Error("{0}. ({1}, {2}). Will retry later.",
                    activityPackage.FailureMessage(),
                    responseString.Trim(),
                    (int)response.StatusCode);
            }

            return new SendResponse
            {
                ResponseData = responseData,
                JsonDict = jsonDict,
            };
        }

        private SendResponse ProcessException(Exception exception, ActivityPackage activityPackage)
        {
            ResponseData responseData = new ResponseData();

            responseData.SetResponseError(exception.Message);
            responseData.WillRetry = true;

            Logger.Error("{0}. ({1}). Will retry later", activityPackage.FailureMessage(), exception.Message);

            return new SendResponse
            {
                ResponseData = responseData,
            };
        }

        private void PackageSent(Task<SendResponse> SendTask)
        {
            // status needs to be tested before reading the result.
            // section "Passing data to a continuation" of
            // http://msdn.microsoft.com/en-us/library/ee372288(v=vs.110).aspx
            var successRunning =
                !SendTask.IsFaulted
                && !SendTask.IsCanceled;

            if (successRunning)
                PackageHandler.FinishedTrackingActivity(
                    activityPackage: SendTask.Result.ActivtyPackage,
                    responseData: SendTask.Result.ResponseData,
                    jsonDict: SendTask.Result.JsonDict);

            if (successRunning && !SendTask.Result.ResponseData.WillRetry)
                PackageHandler.SendNextPackage();
            else
                PackageHandler.CloseFirstPackage();
        }
    }
}