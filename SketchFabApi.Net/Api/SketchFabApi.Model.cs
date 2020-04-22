﻿//
// SketchFabApi.Model.cs
//
// Author:
//       Xavier Fischer 2020-4
//
// Copyright (c) 2020 Xavier Fischer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace SketchFab
{
    public partial class SketchFabApi
    {
        public async Task<SketchFabUploadResponse> UploadModelAsync(UploadModelRequest request, string sketchFabToken)
        {
            SketchFabUploadResponse sfResponse = new SketchFabUploadResponse();
            try
            {
                _logger.LogInformation($"Uploading model [{request.FilePath}].");
                if (string.IsNullOrWhiteSpace(request.FilePath))
                {
                    throw new ArgumentNullException(nameof(request.FilePath));
                }

                if (!File.Exists(request.FilePath))
                {
                    throw new FileNotFoundException($"File [{request.FilePath}] not found.");
                }
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"{SketchFabApiUrl}/models");
                httpRequestMessage.AddAuthorizationHeader(sketchFabToken, request.TokenType);
                using var form = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(request.FilePath));
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
                form.Add(fileContent, "modelFile", Path.GetFileName(request.FilePath));
                if (!string.IsNullOrWhiteSpace(request.Source))
                {
                    form.Add(new StringContent(request.Source), "source");
                }
                else
                {
                    _logger.LogWarning("SketchFab upload has no source configured. It's better to set one to uniquely identify all the models generated by the exporter, see https://sketchfab.com/developers/guidelines#source");
                }

                AddCommonModelFields(form, request);

                httpRequestMessage.Content = form;


                var response = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseContentRead);
                _logger.LogInformation($"{nameof(UploadModelAsync)} responded {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var uuid = response.Headers.GetValues("Location").FirstOrDefault();
                    sfResponse.ModelId = uuid;
                    sfResponse.StatusCode = response.StatusCode;
                    sfResponse.Message = response.ReasonPhrase;
                    request.ModelId = uuid;
                    _logger.LogInformation("Uploading is complete. Model uuid is " + uuid);
                }
                else
                {
                    _logger.LogError($"Error in SketchFab upload: {response.StatusCode} {response.ReasonPhrase}");
                    sfResponse.StatusCode = response.StatusCode;
                    sfResponse.Message = response.ReasonPhrase;
                }

                return sfResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"SketchFab upload error: {ex.Message}");
                throw;
            }

        }

        private string GetAuthPrefix(UploadModelRequest request)
        {
            return request.TokenType.ToString();
        }

        public async Task UpdateModelAsync(string modelId, UploadModelRequest request, string sketchFabToken)
        {
            try
            {
                _logger.LogInformation($"Updating model [{request.Name}].");
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    throw new ArgumentNullException(nameof(modelId));
                }

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Patch, $"{SketchFabApiUrl}/models/{modelId}");
                httpRequestMessage.AddAuthorizationHeader(sketchFabToken, request.TokenType);

                using var form = new MultipartFormDataContent();
                form.Headers.ContentType.MediaType = "multipart/form-data";

                AddCommonModelFields(form, request);

                httpRequestMessage.Content = form;

                var response = await _httpClient.SendAsync(httpRequestMessage);

                _logger.LogInformation($"{nameof(UpdateModelAsync)} responded {response.StatusCode}");
                response.EnsureSuccessStatusCode();

            }
            catch (Exception ex)
            {
                _logger.LogError($"SketchFab update error: {ex.Message}");
                throw;
            }

        }

        public async Task<Model> GetModelAsync(string modelId)
        {
            try
            {
                _logger.LogInformation($"Get model");

                if (string.IsNullOrWhiteSpace(modelId)) throw new ArgumentNullException(nameof(modelId));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, $"{SketchFabApiUrl}/models/{modelId}");

                var response = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseContentRead);
                _logger.LogInformation($"{nameof(GetModelAsync)} responded {response.StatusCode}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                var model = JsonConvert.DeserializeObject<Model>(json);

                _logger.LogInformation($"GetModelAsync OK");

                return model;
            }
            catch (Exception ex)
            {
                _logger.LogError($"SketchFab GetModelAsync error: {ex.Message}");
                throw;
            }

        }

        public async Task<bool> IsReadyAsync(string modelId)
        {
            try
            {
                var model = await this.GetModelAsync(modelId);

                return model.IsReady();
            }
            catch (Exception ex)
            {
                _logger.LogError($"SketchFab IsReadyAsync error: {ex.Message}");
                throw;
            }
        }
    }
}
