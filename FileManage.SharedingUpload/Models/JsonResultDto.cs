﻿namespace FileManage.SharedingUpload.Models
{
    public class JsonResultDto
    {
        public static CommResult Error(string errMsg)
        {
            var result = new CommResult();
            result.Code = -1;
            result.Message = errMsg;
            result.Result = false;
            return result;
        }

        public static CommResult Success()
        {
            var result = new CommResult();
            result.Result = true;
            result.Code = 0;
            return result;
        }

        public static CommResult Success(object resultData)
        {
            var result = new CommResult();
            result.Result = true;
            result.Code = 0;
            result.Data = resultData;
            return result;
        }

        public static CommResult Success(string v)
        {
            var result = new CommResult();
            result.Result = true;
            result.Code = 0;
            result.Message = v;
            return result;
        }
    }

    public class CommResult
    {
        public bool Result { get; set; }

        public object Data { get; set; }

        public int Code { get; set; }

        public string Message { get; set; }
    }
}