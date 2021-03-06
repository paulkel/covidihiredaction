// THIS SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.


using System.Text.RegularExpressions;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Configuration;

using SkiaSharp;

using Windows.Storage;
using Windows.UI.Xaml;

namespace ComputerVisionQuickstart
{
    class Program
    {
        
        public sealed class Settings
        {
            public string SubscriptionKey { get; set; } = "";
            public string Endpoint { get; set; } = "";
            public string SourceImageUrl { get; set; } = "";
        }
            
        static void Main(string[] args)
        {
            
            IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json").AddEnvironmentVariables().Build();
            Settings settings = config.GetRequiredSection("Settings").Get<Settings>();
            
            // Instantiate an Azure Computer Vision Client
            ComputerVisionClient client = Authenticate(settings.Endpoint, settings.SubscriptionKey);

            // Extract text (OCR) from a URL image using the Read API and redact it
            ReadFileUrl(client, settings.SourceImageUrl).Wait();
        }

        

        public static ComputerVisionClient Authenticate(string endpoint, string key)
        {
            ComputerVisionClient client =
              new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
              { Endpoint = endpoint };
            return client;
        }

        public static async Task ReadFileUrl(ComputerVisionClient client, string urlFile)
        {
            ReadOperationResult readResult = new();
            MemoryStream imageMemoryStreamForAnalysis = new();
            MemoryStream imageMemoryStreamForManipulation = new();
            Regex pattern = new Regex(@"^(\d{4} ){3}\d{4}$");

            HttpResponseMessage response = await new System.Net.Http.HttpClient().GetAsync(urlFile);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsByteArrayAsync();
                imageMemoryStreamForAnalysis = new MemoryStream(content);
                imageMemoryStreamForManipulation = new MemoryStream(content);
            }
            else
            {
                Console.WriteLine("The request failed with status code: " + response.StatusCode);
                // Print the headers - they include the request ID and the timestamp, which are useful for debugging the failure

            }

            var textHeaders = await client.ReadInStreamAsync(imageMemoryStreamForAnalysis);


            string operationLocation = textHeaders.OperationLocation;
            
            const int numberOfCharsInOperationId = 36;
            Guid operationId = Guid.Parse(operationLocation.Substring(operationLocation.Length - numberOfCharsInOperationId));
            Console.WriteLine(operationId);

            do {
                readResult = await client.GetReadResultAsync(operationId);
                if (readResult.Status == OperationStatusCodes.Succeeded || readResult.Status == OperationStatusCodes.Failed)
                {
                    break;
                }
                Thread.Sleep(1000);
   
            } while (readResult.Status == OperationStatusCodes.Running || readResult.Status == OperationStatusCodes.NotStarted);

            if (readResult.Status == OperationStatusCodes.Succeeded)
            {
                foreach (ReadResult item in readResult.AnalyzeResult.ReadResults) {
                    foreach (Line line in item.Lines) {
                        if (pattern.IsMatch(line.Text)) {
                            List<float> ihiBbox = new();
                            foreach (var coordinate in line.BoundingBox)
                            {
                                ihiBbox.Add((float) coordinate!);
                            }
                           
                            float width = Math.Max(ihiBbox[2], ihiBbox[4]) - Math.Min(ihiBbox[0], ihiBbox[6]);
                            float height = Math.Max(ihiBbox[5], ihiBbox[7]) - Math.Min(ihiBbox[1], ihiBbox[3]);
                             
                            SKBitmap imageBitmap = new();
                            imageBitmap = SKBitmap.Decode(imageMemoryStreamForManipulation);
                            
                            SKCanvas imageCanvas = new SKCanvas(imageBitmap);
                            imageCanvas.DrawRect(ihiBbox[0], ihiBbox[1], width, height, new SKPaint() { Color = SKColors.Black, Style = SKPaintStyle.Fill });
                            
                            try
                            {
                                StorageFile storageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("covidcert.jpg", CreationCollisionOption.ReplaceExisting);
                                //File.OpenWrite(storageFile.Path);
                                Stream fileStream = await storageFile.OpenStreamForWriteAsync();
                                imageBitmap.Encode(SKEncodedImageFormat.Jpeg, 100).SaveTo(fileStream);
                                fileStream.Close();

                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }


                        }
                    }
                }
                
            }
            else
            {
                Console.WriteLine("Computer Vision analysis failed. Please try again.");
            }
            
            
        }

    }
}