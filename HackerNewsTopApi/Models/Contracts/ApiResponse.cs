namespace HackerNewsTopApi.Models.Contracts
{
    public class PaginationMetadata
    {
        public int Page { get; set; }
        public int Limit { get; set; }
        public int Returned { get; set; }
        public long? Total { get; set; }
    }

    public class ApiResponse<T>
    {
        public T Data { get; set; } = default!;
        public PaginationMetadata? Meta { get; set; }
        public IDictionary<string, string>? Links { get; set; }

        public ApiResponse(T data)
        {
            Data = data;
        }
    }
}
