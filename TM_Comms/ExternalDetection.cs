using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace TM_Comms
{
    public class ExternalDetection
    {
        public class DetectionResponse
        {
            public Annotation[] annotations { get; set; }
            public string message { get; set; } = "success";

            public DetectionResponse() { }

            public DetectionResponse(int annotationCount)
            {
                annotations = new Annotation[annotationCount];
                for(int i = 0; i < annotationCount; i++)
                    annotations[i] = new Annotation();
            }
        }

        public class Annotation : INotifyPropertyChanged
        {
            private int _box_cx;
            private int _box_cy;
            private int _box_h;
            private int _box_w;
            private string _label = string.Empty;
            private int _rotation;
            private float _score;

            public int box_cx { get { return _box_cx; } set { _box_cx = value; RaisePropertyChanged("box_cx"); } }
            public int box_cy { get { return _box_cy; } set { _box_cy = value; RaisePropertyChanged("box_cy"); } }
            public int box_h { get { return _box_h; } set { _box_h = value; RaisePropertyChanged("box_h"); } }
            public int box_w { get { return _box_w; } set { _box_w = value; RaisePropertyChanged("box_w"); } }
            public string label { get { return _label; } set { _label = value; RaisePropertyChanged("label"); } }
            public int rotation { get { return _rotation; } set { _rotation = value; RaisePropertyChanged("rotation"); } }
            public float score { get { return _score; } set { _score = value; RaisePropertyChanged("score"); } }

            public event PropertyChangedEventHandler PropertyChanged;
            private void RaisePropertyChanged(string propertyName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class ClassifyResponse : INotifyPropertyChanged
        {
            string _message= "success";
            string _result;
            float _score;

            public string message { get { return _message; } set { _message = value; RaisePropertyChanged("message"); } } 
            public string result { get { return _result; } set { _result = value; RaisePropertyChanged("result"); } }
            public float score { get { return _score; } set { _score = value; RaisePropertyChanged("score"); } }

            public event PropertyChangedEventHandler PropertyChanged;
            private void RaisePropertyChanged(string propertyName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        //public static BitmapImage ProcessMessageForImage(string message)
        //{
        //    Match boundry = Regex.Match(message, @"(?<=boundary=).*");
        //    string boundaryStart = $"(?=--{boundry.Value})(?s).*";

        //    Match body = Regex.Match(message, boundaryStart);

        //    MultipartParser parser = new MultipartParser(body.Value);

        //    if(parser.Success)
        //    {
        //        MemoryStream ms = new MemoryStream(parser.FileContents);

        //        var imageSource = new BitmapImage();
        //        imageSource.BeginInit();
        //        imageSource.StreamSource = ms;
        //        imageSource.EndInit();
        //        imageSource.Freeze();
        //        return imageSource;

        //    }
        //    else
        //        return null;
        //}
        //public static BitmapImage ProcessMessageForImage(Stream message)
        //{
        //    MultipartParser parser = new MultipartParser(message);
        //    if(parser.Success)
        //    {
        //        MemoryStream ms = new MemoryStream(parser.FileContents);

        //        var imageSource = new BitmapImage();
        //        imageSource.BeginInit();
        //        imageSource.StreamSource = ms;
        //        imageSource.EndInit();
        //        imageSource.Freeze();
        //        return imageSource;

        //    }
        //    else
        //        return null;

        //    //Match boundry = Regex.Match(message, @"(?<=boundary=).*");
        //    //string boundaryStart = $"(?<=--{boundry.Value})(?s).*";

        //    //Match body = Regex.Match(message, boundaryStart);

        //    //TextReader textReader = new StreamReader(message);
        //    //string sLine = textReader.ReadLine();
        //    //Regex regex = new Regex("(^-+)|(^content-)|(^submit)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        //    //StringBuilder sb = new StringBuilder();

        //    //while(sLine != null)
        //    //{
        //    //    if(!regex.Match(sLine).Success)
        //    //    {
        //    //        break;
        //    //    }
        //    //    sLine = textReader.ReadLine();
        //    //}
        //    //FileStream file = File.OpenWrite("test.jpg");
        //    //byte[] ar = Encoding.ASCII.GetBytes(textReader.ReadToEnd());
        //    //file.Write(ar,0,ar.Length);
        //    //file.Close();
        //    ////byte[] imageBytes = Convert.FromBase64String();
        //    ////MemoryStream ms = new MemoryStream(imageBytes);



        //    //return image;
        //    //}

        //}

        public class MultipartParser
        {
            private byte[] Data;
            private string Content;
            private Encoding Encoding = Encoding.UTF8;
            public MultipartParser(Stream stream)
            {
                ParseStream(stream, Encoding.UTF8);
            }
            public MultipartParser(string message)
            {
                Data = Encoding.GetBytes(message);
                Content = message;
                this.Parse();
            }

            public MultipartParser(Stream stream, Encoding encoding)
            {
                ParseStream(stream, encoding);
            }
            private void ParseStream(Stream stream, Encoding encoding)
            {
                // Read the stream into a byte array
                Data = ToByteArray(stream);

                // Copy to a string for header parsing
                Content = encoding.GetString(Data);

                this.Parse();
            }
            private void Parse()
            {
                this.Success = false;

                // The first line should contain the delimiter
                int delimiterEndIndex = Content.IndexOf("\r\n");

                if(delimiterEndIndex > -1)
                {
                    string delimiter = Content.Substring(0, Content.IndexOf("\r\n"));

                    // Look for Content-Type
                    Regex re = new Regex(@"(?<=Content\-Type:)(.*?)(?=\r\n\r\n)");
                    Match contentTypeMatch = re.Match(Content);

                    // Look for filename
                    re = new Regex(@"(?<=filename\=\"")(.*?)(?=\"")");
                    Match filenameMatch = re.Match(Content);

                    // Did we find the required values?
                    if(contentTypeMatch.Success && filenameMatch.Success)
                    {
                        // Set properties
                        this.ContentType = contentTypeMatch.Value.Trim();
                        this.Filename = filenameMatch.Value.Trim();

                        // Get the start & end indexes of the file contents
                        int startIndex = contentTypeMatch.Index + contentTypeMatch.Length + "\r\n\r\n".Length;

                        byte[] delimiterBytes = Encoding.GetBytes("\r\n" + delimiter);
                        int endIndex = IndexOf(Data, delimiterBytes, startIndex);

                        int contentLength = endIndex - startIndex;

                        // Extract the file contents from the byte array
                        byte[] fileData = new byte[contentLength];

                        Buffer.BlockCopy(Data, startIndex, fileData, 0, contentLength);

                        this.FileContents = fileData;
                        this.Success = true;
                    }
                }
            }

            private int IndexOf(byte[] searchWithin, byte[] serachFor, int startIndex)
            {
                int index = 0;
                int startPos = Array.IndexOf(searchWithin, serachFor[0], startIndex);

                if(startPos != -1)
                {
                    while((startPos + index) < searchWithin.Length)
                    {
                        if(searchWithin[startPos + index] == serachFor[index])
                        {
                            index++;
                            if(index == serachFor.Length)
                            {
                                return startPos;
                            }
                        }
                        else
                        {
                            startPos = Array.IndexOf<byte>(searchWithin, serachFor[0], startPos + index);
                            if(startPos == -1)
                            {
                                return -1;
                            }
                            index = 0;
                        }
                    }
                }

                return -1;
            }

            private byte[] ToByteArray(Stream stream)
            {
                byte[] buffer = new byte[32768];
                using(MemoryStream ms = new MemoryStream())
                {
                    while(true)
                    {
                        int read = stream.Read(buffer, 0, buffer.Length);
                        if(read <= 0)
                            return ms.ToArray();
                        ms.Write(buffer, 0, read);
                    }
                }
            }

            public bool Success
            {
                get;
                private set;
            }

            public string ContentType
            {
                get;
                private set;
            }

            public string Filename
            {
                get;
                private set;
            }

            public byte[] FileContents
            {
                get;
                private set;
            }
        }



    }
}
