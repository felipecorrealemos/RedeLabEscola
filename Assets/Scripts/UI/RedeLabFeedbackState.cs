using System;
using RedeLabEscola.Auth;

namespace RedeLabEscola.UI
{
    public enum RedeLabFeedbackUiState
    {
        Ready,
        Sending,
        Sent,
        Error,
        LoadingHistory,
        HistoryLoaded,
        HistoryEmpty,
        Unauthenticated
    }

    public static class RedeLabFeedbackValidation
    {
        public const int MaximumCommentLength = 1000;
        public const string SuggestionType = "sugestao";
        public const string BugType = "bug";
        public const string CommentType = "comentario";

        public static bool IsSupportedType(string type)
        {
            return string.Equals(type, SuggestionType, StringComparison.Ordinal)
                || string.Equals(type, BugType, StringComparison.Ordinal)
                || string.Equals(type, CommentType, StringComparison.Ordinal);
        }

        public static bool IsValidComment(string comment)
        {
            return !string.IsNullOrWhiteSpace(comment)
                && comment.Length <= MaximumCommentLength;
        }

        public static bool CanSubmit(string type, string comment)
        {
            return IsSupportedType(type) && IsValidComment(comment);
        }

        public static string DisplayLabel(string type)
        {
            if (string.Equals(type, SuggestionType, StringComparison.Ordinal)) return "SUGESTÃO";
            if (string.Equals(type, BugType, StringComparison.Ordinal)) return "BUG";
            return "COMENTÁRIO";
        }

        public static RedeLabFeedback[] NewestFirst(RedeLabFeedback[] feedbacks)
        {
            if (feedbacks == null || feedbacks.Length == 0) return Array.Empty<RedeLabFeedback>();
            RedeLabFeedback[] ordered = (RedeLabFeedback[])feedbacks.Clone();
            Array.Sort(ordered, CompareNewestFirst);
            return ordered;
        }

        private static int CompareNewestFirst(RedeLabFeedback left, RedeLabFeedback right)
        {
            DateTime leftDate = ParseDate(left != null ? left.data_envio : null);
            DateTime rightDate = ParseDate(right != null ? right.data_envio : null);
            int byDate = rightDate.CompareTo(leftDate);
            if (byDate != 0) return byDate;
            long leftId = left != null ? left.id_feedback : 0L;
            long rightId = right != null ? right.id_feedback : 0L;
            return rightId.CompareTo(leftId);
        }

        public static DateTime ParseDate(string value)
        {
            return DateTime.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal
                    | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out DateTime parsed)
                ? parsed
                : DateTime.MinValue;
        }
    }

    public sealed class RedeLabFeedbackDraft
    {
        public string Type { get; private set; } = RedeLabFeedbackValidation.SuggestionType;
        public string Comment { get; private set; } = string.Empty;
        public bool IsSending { get; private set; }
        public bool CanSubmit => !IsSending && RedeLabFeedbackValidation.CanSubmit(Type, Comment);

        public bool SetType(string type)
        {
            if (!RedeLabFeedbackValidation.IsSupportedType(type)) return false;
            Type = type;
            return true;
        }

        public void SetComment(string comment)
        {
            Comment = comment ?? string.Empty;
        }

        public bool TryBeginSubmission(out string trimmedComment)
        {
            trimmedComment = Comment.Trim();
            if (IsSending || !RedeLabFeedbackValidation.CanSubmit(Type, Comment)) return false;
            IsSending = true;
            return true;
        }

        public void CompleteSuccess()
        {
            IsSending = false;
            Comment = string.Empty;
        }

        public void CompleteFailure()
        {
            IsSending = false;
        }
    }
}
