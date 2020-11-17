namespace GrpcClient.ServerNet
{
    /// <summary>
    /// 传输结果
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class TransferResult<T>
    {
        /// <summary>
        /// 传输是否成功
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 标记类型
        /// </summary>
        public T Tag { get; set; } = default;
    }
}