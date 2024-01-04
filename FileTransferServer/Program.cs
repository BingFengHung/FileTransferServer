using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace FileTransferServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            StartServer();
        }

        public static void StartServer()
        {
            IPAddress ip = IPAddress.Parse("127.0.0.1");
            int port = 8000;
            TcpListener server = new TcpListener(ip, port);

            server.Start();
            Console.WriteLine("Server started. Waiting for connections...");

            try
            {
                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Connected to client.");

                    // 處理客戶端連線在一個新執行緒
                    Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClient));
                    clientThread.Start(client);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                server.Stop();
            }
        }

        // 讀取指定數量的字節，直到達到所需的數量或流結束
        private static int ReadFull(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int bytesRead = stream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    break;
                }
                totalBytesRead += bytesRead;
            }
            return totalBytesRead;
        }

        private static void HandleClient(object obj)
        {
            TcpClient client = (TcpClient)obj;
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[4096];
            int bytesRead;

            try
            {
                while (true)
                {

                    // 接收檔案名稱長度
                    bytesRead = ReadFull(stream, buffer, 0, 4);

                    if (bytesRead != 4)
                        throw new InvalidOperationException("無法從網路流中讀取完整的檔案名稱長度資訊");

                    int fileNameLen = BitConverter.ToInt32(buffer, 0);

                    if (fileNameLen == -1) // 結束了
                        break;

                    // 接收檔案名稱
                    bytesRead = ReadFull(stream, buffer, 0, fileNameLen);
                    if (bytesRead != fileNameLen)
                        throw new InvalidOperationException("無法從網路流中讀取完整的檔案名稱");
                    string fileName = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // 接收檔案大小
                    bytesRead = ReadFull(stream, buffer, 0, 4);

                    if (bytesRead != 4)
                        throw new InvalidOperationException("無法從網路流中讀取完整的檔案大小");
                    int fileDataLen = BitConverter.ToInt32(buffer, 0);
                    Console.WriteLine(fileDataLen);

                    if (fileDataLen < 0)
                    {
                        throw new InvalidOperationException("檔案大小不能為負數。");
                    }

                    // 創建檔案
                    string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        int totalBytesRead = 0;
                        while (totalBytesRead < fileDataLen)
                        {
                            int toRead = Math.Min(buffer.Length, fileDataLen - totalBytesRead);
                            bytesRead = ReadFull(stream, buffer, 0, toRead);

                            if (bytesRead == 0)
                            {
                                throw new InvalidOperationException("網絡流提前結束，無法讀取完整的檔案數據。");
                            }

                            fileStream.Write(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                // 關閉連線
                stream.Close();
                client.Close();
            }
        }
    }
}
