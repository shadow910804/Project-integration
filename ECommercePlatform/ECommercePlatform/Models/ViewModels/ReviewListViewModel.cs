using ECommercePlatform.Models;

namespace ECommercePlatform.Models.ViewModels
{
    public class ReviewListViewModel
    {
        public List<Review> Reviews { get; set; } = new();
        public int TotalItems { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalReviews { get; set; }
        public double AverageScore { get; set; }
        public Dictionary<int, int> RatingDistribution { get; set; } = new();

        // 新增統計屬性
        public int RecentReviewsCount { get; set; }
        public int WithImagesCount { get; set; }
        public int WithAdminReplyCount { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        // 評分分佈百分比
        public Dictionary<int, double> RatingPercentages
        {
            get
            {
                if (TotalReviews == 0) return new Dictionary<int, double>();
                return RatingDistribution.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (double)kvp.Value / TotalReviews * 100
                );
            }
        }
    }
}