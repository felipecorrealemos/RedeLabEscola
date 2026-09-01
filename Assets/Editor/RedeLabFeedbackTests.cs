using NUnit.Framework;
using RedeLabEscola.Auth;
using RedeLabEscola.UI;
using TMPro;
using UnityEngine;

public sealed class RedeLabFeedbackTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   \n\t")]
    public void EmptyOrWhitespaceCommentIsRejected(string comment)
    {
        Assert.IsFalse(RedeLabFeedbackValidation.CanSubmit(RedeLabFeedbackValidation.SuggestionType, comment));
    }

    [Test]
    public void CommentAtLimitIsAccepted()
    {
        Assert.IsTrue(RedeLabFeedbackValidation.CanSubmit(
            RedeLabFeedbackValidation.BugType,
            new string('a', RedeLabFeedbackValidation.MaximumCommentLength)));
    }

    [Test]
    public void CommentAboveLimitIsRejected()
    {
        Assert.IsFalse(RedeLabFeedbackValidation.CanSubmit(
            RedeLabFeedbackValidation.CommentType,
            new string('a', RedeLabFeedbackValidation.MaximumCommentLength + 1)));
    }

    [Test]
    public void UnsupportedTypeIsRejected()
    {
        Assert.IsFalse(RedeLabFeedbackValidation.CanSubmit("outro", "Comentário válido"));
    }

    [Test]
    public void DraftPreventsDuplicateSubmission()
    {
        RedeLabFeedbackDraft draft = new RedeLabFeedbackDraft();
        draft.SetComment("Primeiro envio");

        Assert.IsTrue(draft.TryBeginSubmission(out string first));
        Assert.AreEqual("Primeiro envio", first);
        Assert.IsFalse(draft.TryBeginSubmission(out _));
    }

    [Test]
    public void FailedSubmissionPreservesDraft()
    {
        RedeLabFeedbackDraft draft = new RedeLabFeedbackDraft();
        draft.SetComment("Não perder este texto");
        Assert.IsTrue(draft.TryBeginSubmission(out _));

        draft.CompleteFailure();

        Assert.AreEqual("Não perder este texto", draft.Comment);
        Assert.IsTrue(draft.CanSubmit);
    }

    [Test]
    public void SuccessfulSubmissionClearsDraftAndAllowsAnother()
    {
        RedeLabFeedbackDraft draft = new RedeLabFeedbackDraft();
        draft.SetComment("Enviado");
        Assert.IsTrue(draft.TryBeginSubmission(out _));

        draft.CompleteSuccess();

        Assert.AreEqual(string.Empty, draft.Comment);
        Assert.IsFalse(draft.IsSending);
        draft.SetComment("Novo comentário");
        Assert.IsTrue(draft.CanSubmit);
    }

    [Test]
    public void HistoryIsSortedNewestFirst()
    {
        RedeLabFeedback[] ordered = RedeLabFeedbackValidation.NewestFirst(new[]
        {
            Feedback(1, "2026-08-30T10:00:00Z"),
            Feedback(2, "2026-09-01T09:00:00Z"),
            Feedback(3, "2026-09-01T09:00:00Z")
        });

        CollectionAssert.AreEqual(new long[] { 3, 2, 1 }, new[]
        {
            ordered[0].id_feedback,
            ordered[1].id_feedback,
            ordered[2].id_feedback
        });
    }

    [Test]
    public void RuntimePanelHasExpectedInputConfiguration()
    {
        GameObject card = new GameObject("FeedbackTestCard", typeof(RectTransform));
        try
        {
            RedeLabFeedbackPanel panel = card.AddComponent<RedeLabFeedbackPanel>();
            panel.Build();

            Assert.NotNull(panel.CommentInput);
            Assert.AreEqual(1000, panel.CommentInput.characterLimit);
            Assert.AreEqual(TMP_InputField.LineType.MultiLineNewline, panel.CommentInput.lineType);
            Assert.IsFalse(panel.HistoryPanel.activeSelf);
            Assert.AreEqual(Application.version, RedeLabFeedbackPanel.CurrentGameVersion);
        }
        finally
        {
            Object.DestroyImmediate(card);
        }
    }

    private static RedeLabFeedback Feedback(long id, string date)
    {
        return new RedeLabFeedback
        {
            id_feedback = id,
            tipo = RedeLabFeedbackValidation.SuggestionType,
            comentario = "Teste",
            versao_jogo = "1.0",
            data_envio = date
        };
    }
}
