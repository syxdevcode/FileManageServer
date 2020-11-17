using Grpc.Core;
using GrpcService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcClient.ServerNet
{
    internal class FileTransfer
    {
        /// <summary>
        /// 获取通信客户端
        /// </summary>
        /// <returns>通信频道、客户端</returns>
        private static (Channel, GrpcService.FileTransfer.FileTransferClient) GetClient()
        {
            //侦听IP和端口要和服务器一致
            Channel channel = new Channel("127.0.0.1", 50000, ChannelCredentials.Insecure);
            var client = new GrpcService.FileTransfer.FileTransferClient(channel);
            return (channel, client);
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="fileNames">需要下载的文件集合</param>
        /// <param name="mark">标记</param>
        /// <param name="saveDirectoryPath">保存路径</param>
        /// <param name="cancellationToken">异步取消命令</param>
        /// <returns>下载任务（是否成功、原因、失败文件名）</returns>
        public static async Task<TransferResult<List<string>>> FileDownload(List<string> fileNames, string mark, string saveDirectoryPath, System.Threading.CancellationToken cancellationToken = new System.Threading.CancellationToken())
        {
            var result = new TransferResult<List<string>>() { Message = $"文件保存路径不正确：{saveDirectoryPath}" };
            if (!System.IO.Directory.Exists(saveDirectoryPath))
            {
                return await Task.Run(() => result);//文件路径不存在
            }
            if (fileNames.Count == 0)
            {
                result.Message = "未包含任何文件";
                return await Task.Run(() => result);//文件路径不存在
            }
            result.Message = "未能连接到服务器";
            FileRequest request = new FileRequest() { Mark = mark };//请求数据
            request.FileNames.AddRange(fileNames);//将需要下载的文件名赋值
            var lstSuccFiles = new List<string>();//传输成功的文件
            string savePath = string.Empty;//保存路径
            System.IO.FileStream fs = null;
            Channel channel = null;//申明通信频道
            GrpcService.FileTransfer.FileTransferClient client = null;
            DateTime startTime = DateTime.Now;
            try
            {
                (channel, client) = GetClient();
                using (var call = client.FileDownload(request))
                {
                    List<FileReply> lstContents = new List<FileReply>();//存放接收的数据
                    var reaponseStream = call.ResponseStream;
                    //reaponseStream.Current.Block数字的含义是服务器和客户端约定的
                    while (await reaponseStream.MoveNext(cancellationToken))//开始接收数据
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        if (reaponseStream.Current.Block == -2)//说明文件已经传输完成了
                        {
                            result.Message = $"完成下载任务【{lstSuccFiles.Count}/{fileNames.Count}】，耗时：{DateTime.Now - startTime}";
                            result.IsSuccessful = true;
                            break;
                        }
                        else if (reaponseStream.Current.Block == -1)//当前文件传输错误
                        {
                            Console.WriteLine($"文件【{reaponseStream.Current.FileName}】传输失败！");//写入日志
                            lstContents.Clear();
                            fs?.Close();//释放文件流
                            if (!string.IsNullOrEmpty(savePath) && File.Exists(savePath))//如果传输不成功，删除该文件
                            {
                                File.Delete(savePath);
                            }
                            savePath = string.Empty;
                        }
                        else if (reaponseStream.Current.Block == 0)//当前文件传输完成
                        {
                            if (lstContents.Any())//如果还有数据，就写入文件
                            {
                                lstContents.OrderBy(c => c.Block).ToList().ForEach(c => c.Content.WriteTo(fs));
                                lstContents.Clear();
                            }
                            lstSuccFiles.Add(reaponseStream.Current.FileName);//传输成功的文件
                            fs?.Close();//释放文件流
                            savePath = string.Empty;
                        }
                        else//有文件数据过来
                        {
                            if (string.IsNullOrEmpty(savePath))//如果字节流为空，则说明时新的文件数据来了
                            {
                                savePath = Path.Combine(saveDirectoryPath, reaponseStream.Current.FileName);
                                fs = new FileStream(savePath, FileMode.Create, FileAccess.ReadWrite);
                            }
                            lstContents.Add(reaponseStream.Current);//加入链表
                            if (lstContents.Count() >= 20)//每个包1M，20M为一个集合，一起写入数据。
                            {
                                lstContents.OrderBy(c => c.Block).ToList().ForEach(c => c.Content.WriteTo(fs));
                                lstContents.Clear();
                            }
                        }
                    }
                }
                fs?.Close();//释放文件流
                if (!result.IsSuccessful && !string.IsNullOrEmpty(savePath) && File.Exists(savePath))//如果传输不成功，那么久删除该文件
                {
                    File.Delete(savePath);
                }
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    fs?.Close();//释放文件流
                    result.IsSuccessful = false;
                    result.Message = $"用户取消下载。已完成下载【{lstSuccFiles.Count}/{fileNames.Count}】，耗时：{DateTime.Now - startTime}";
                }
                else
                {
                    result.Message = $"文件传输发生异常：{ex.Message}";
                }
            }
            finally
            {
                fs?.Dispose();
            }
            result.Tag = fileNames.Except(lstSuccFiles).ToList();//获取失败文件集合
            //关闭通信、并返回结果
            return await channel?.ShutdownAsync().ContinueWith(t => result);
        }

        /// <summary>
        /// 文件上传
        /// </summary>
        /// <param name="filesPath">文件路径</param>
        /// <param name="mark">标记</param>
        /// <param name="cancellationToken">异步取消命令</param>
        /// <returns>下载任务（是否成功、原因、成功的文件名）</returns>
        public static async Task<TransferResult<List<string>>> FileUpload(List<string> filesPath, string mark, System.Threading.CancellationToken cancellationToken = new System.Threading.CancellationToken())
        {
            var result = new TransferResult<List<string>> { Message = "没有文件需要下载" };
            if (filesPath.Count == 0)
            {
                return await Task.Run(() => result);//没有文件需要下载
            }
            result.Message = "未能连接到服务器。";
            var lstSuccFiles = new List<string>();//传输成功的文件
            int chunkSize = 1024 * 1024;
            byte[] buffer = new byte[chunkSize];//每次发送的大小
            FileStream fs = null;//文件流
            Channel channel = null;//申明通信频道
            GrpcService.FileTransfer.FileTransferClient client = null;
            DateTime startTime = DateTime.Now;
            try
            {
                (channel, client) = GetClient();
                using (var stream = client.FileUpload())//连接上传文件的客户端
                {
                    //reply.Block数字的含义是服务器和客户端约定的
                    foreach (var filePath in filesPath)//遍历集合
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;//取消了传输
                        FileReply reply = new FileReply()
                        {
                            FileName = Path.GetFileName(filePath),
                            Mark = mark
                        };
                        if (!File.Exists(filePath))//文件不存在，继续下一轮的发送
                        {
                            Console.WriteLine($"文件不存在：{filePath}");//写入日志
                            continue;
                        }
                        fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize, useAsync: true);
                        int readTimes = 0;
                        while (true)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                reply.Block = -1;//取消了传输
                                reply.Content = Google.Protobuf.ByteString.Empty;
                                await stream.RequestStream.WriteAsync(reply);//发送取消传输的命令
                                break;//取消了传输
                            }
                            int readSize = fs.Read(buffer, 0, buffer.Length);//读取数据
                            if (readSize > 0)
                            {
                                reply.Block = ++readTimes;//更新标记，发送数据
                                reply.Content = Google.Protobuf.ByteString.CopyFrom(buffer, 0, readSize);
                                await stream.RequestStream.WriteAsync(reply);
                            }
                            else
                            {
                                Console.WriteLine($"完成文件【{filePath}】的上传。");
                                reply.Block = 0;//传送本次文件发送结束的标记
                                reply.Content = Google.Protobuf.ByteString.Empty;
                                await stream.RequestStream.WriteAsync(reply);//发送结束标记
                                //等待服务器回传
                                await stream.ResponseStream.MoveNext(cancellationToken);
                                if (stream.ResponseStream.Current != null && stream.ResponseStream.Current.Mark == mark)
                                {
                                    lstSuccFiles.Add(filePath);//记录成功的文件
                                }
                                break;//发送下一个文件
                            }
                        }
                        fs?.Close();
                    }
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        result.IsSuccessful = true;
                        result.Message = $"完成文件上传。共计【{lstSuccFiles.Count}/{filesPath.Count}】，耗时:{DateTime.Now - startTime}";

                        await stream.RequestStream.WriteAsync(new FileReply
                        {
                            Block = -2,//传输结束
                            Mark = mark
                        });//发送结束标记
                    }
                }
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    fs?.Close();//释放文件流
                    result.IsSuccessful = false;
                    result.Message = $"用户取消了上传文件。已完成【{lstSuccFiles.Count}/{filesPath.Count}】，耗时:{DateTime.Now - startTime}";
                }
                else
                {
                    result.Message = $"文件上传发生异常({ex.GetType()})：{ex.Message}";
                }
            }
            finally
            {
                fs?.Dispose();
            }
            Console.WriteLine(result.Message);
            result.Tag = lstSuccFiles;
            //关闭通信、并返回结果
            return await channel?.ShutdownAsync().ContinueWith(t => result);
        }
    }
}