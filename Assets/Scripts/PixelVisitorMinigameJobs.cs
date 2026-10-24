// ---- System notes ----
// PixelVisitorMinigameJobs.cs
//   Part of PixelVisitorMinigame: a vendor's DELIVERY JOB during a visit (2026-10-24; the jobs themselves: PixelDeliveryJobs).
//   - A 'Delivery job' button at the bottom left of the wares window (beside Done), for every vendor with a dock in PixelDeliveryJobs:
//       * he has an offer this visit (rolled once when the window is built: RollOffer) -> 'Delivery job!' (green) opens the JOB WINDOW;
//       * his job is running -> 'Job: 18 / 40' opens it too, showing how it is going;
//       * nothing (the hourly limit, his crate still out, no luck) -> the button is grey and says why on the window.
//   - The JOB WINDOW (the visit's standard dialog box, in place of the wares window): his 'job offer' line (Dialogue.txt [Visitor: id]),
//     what to deliver and by when, where (his dock), the pay; Accept (drops the crate on his dock) / Back.
//   - Reward items are one of his own wares (RollJobRewardItem: one he sells that you have room for).
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract partial class PixelVisitorMinigame
{
    [Header("Delivery jobs (PixelDeliveryJobs)")]
    [Tooltip("What he says when he offers a job. Empty = the built-in line. Dialogue.txt [Visitor: id] 'job offer' wins.")]
    [SerializeField] private string jobOfferLine = "";
    [Tooltip("Label of the job button in the wares window.")]
    [SerializeField] private string jobButtonText = "Delivery job";

    private PixelDeliveryJobs.Offer jobOffer;
    private Button jobButton;
    private TMP_Text jobButtonLabel;
    private RectTransform jobPanel;
    private TMP_Text jobPanelText;
    private Button jobAccept;

    /// <summary>His name as shown (the delivery crate, the Goals tab).</summary>
    public string VendorName => Pick(visitorName, DefaultName);

    /// <summary>His built-in job line (a vendor can override it; the Dialogue.txt line wins).</summary>
    protected virtual string DefaultJobOffer => "I've got a job for you, if you're up for it.";

    private string JobOfferLine => Pick(jobOfferLine, DefaultJobOffer);

    private void ApplyJobDialogue(PixelTextFile f, string section)
    {
        f.ApplyOr(section, "job offer", ref jobOfferLine, JobOfferLine);
    }

    /// <summary>One of his wares you have room for (a job's reward item), by display name; null = none.</summary>
    private string RollJobRewardItem()
    {
        if (consumables == null) return null;
        List<int> wares = Wares();
        wares.RemoveAll(i => consumables.ItemRoom(i) < 1);
        return wares.Count > 0 ? consumables.ItemName(wares[Random.Range(0, wares.Count)]) : null;
    }

    /// <summary>The job button in the wares window and its job window (called when the wares window is built, once per visit).</summary>
    private void BuildJobParts()
    {
        jobOffer = null;
        jobButton = null;
        jobPanel = null;
        PixelDeliveryJobs jobs = PixelDeliveryJobs.Instance;
        if (jobs == null || !jobs.HasDock(Id) || waresPanel == null) return;
        jobOffer = jobs.RollOffer(Id, VendorName, RollJobRewardItem);

        jobButton = PixelUIKit.CreateButton(Font, waresPanel, "Job", jobButtonText, new Vector2(250f, 80f), ButtonRole.Success, 30f);
        jobButtonLabel = jobButton.GetComponentInChildren<TMP_Text>();
        RectTransform br = jobButton.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = Vector2.zero;
        br.anchoredPosition = new Vector2(24f, 20f);
        jobButton.onClick.AddListener(() => ShowJobPanel(true));

        jobPanel = Panel("Delivery Job", "Delivery Job", new Vector2(900f, 560f), visitorRect.sizeDelta.x + 90f).Rect;
        float top = PixelUIKit.WindowHeaderHeight;
        jobPanelText = PixelUIKit.CreateText(Font, jobPanel, "Job Text", "", 30f, TextAlignmentOptions.TopLeft, FontStyles.Normal, Color.white);
        Anchor(jobPanelText.rectTransform, 0f, 0f, 1f, 1f, new Vector2(40f, 130f), new Vector2(-40f, -(top + 20f)));
        jobAccept = PixelUIKit.CreateButton(Font, jobPanel, "Accept Job", "Accept", new Vector2(300f, 80f), ButtonRole.Success, 36f);
        PixelUIKit.Caps(jobAccept);
        Corner(jobAccept.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-170f, 24f));
        jobAccept.onClick.AddListener(AcceptJob);
        Button back = PixelUIKit.CreateButton(Font, jobPanel, "Back", "Back", new Vector2(300f, 80f), ButtonRole.Neutral, 36f);
        PixelUIKit.Caps(back);
        Corner(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(170f, 24f));
        back.onClick.AddListener(() => ShowJobPanel(false));
        jobPanel.gameObject.SetActive(false);
        RefreshJobParts();
    }

    /// <summary>The button's text / colour and the job window's text for what is going on now.</summary>
    private void RefreshJobParts()
    {
        PixelDeliveryJobs jobs = PixelDeliveryJobs.Instance;
        if (jobButton == null || jobs == null) return;
        DeliveryState.Job running = PixelDeliveryJobs.JobOf(Id);
        string why = "";
        bool canOffer = running == null && jobOffer != null && jobs.CanOffer(Id, out why);
        if (running == null && jobOffer == null) jobs.CanOffer(Id, out why);
        if (running != null)
        {
            int need = 0, have = 0;
            foreach (DeliveryState.Line l in running.lines) { need += l.need; have += Mathf.Min(l.have, l.need); }
            PixelUIKit.SetText(jobButtonLabel, "Job: " + have + " / " + need);
            PixelUIKit.SetRole(jobButton, ButtonRole.Primary);
            PixelUIKit.SetText(jobPanelText, "<b>Your job for " + VendorName + "</b>\n\nFill the crate on " + VendorName + "'s dock with " + jobs.NeedText(running.lines, true)
                + ".\nTime left: <b>" + PixelDeliveryJobs.TimeText(PixelDeliveryJobs.TimeLeft(running)) + "</b>\nPay: " + jobs.PayText(running.pay, running.payType, running.rewardItem)
                + "\n\n<color=#9FB0CC>Feed it with belts or a hose through its In coupling, or drop husks into its open top.</color>");
            jobAccept.gameObject.SetActive(false);
        }
        else if (canOffer)
        {
            PixelUIKit.SetText(jobButtonLabel, jobButtonText + "!");
            PixelUIKit.SetRole(jobButton, ButtonRole.Success);
            PixelUIKit.SetText(jobPanelText, "\"" + JobOfferLine + "\"\n\nDeliver <b>" + jobs.NeedText(jobOffer.lines) + "</b> within <b>"
                + Mathf.RoundToInt(jobOffer.seconds / 60f) + " minutes</b> to the crate I'll drop on my dock (the marked spot at the floor's edge).\n\n"
                + "Pay: <b>" + jobs.PayText(jobOffer.pay, jobOffer.payType, jobOffer.rewardItem) + "</b>\n\n"
                + "<color=#9FB0CC>Out of time? No penalty: the husks come back out.</color>");
            jobAccept.gameObject.SetActive(true);
        }
        else
        {
            PixelUIKit.SetText(jobButtonLabel, jobButtonText);
            PixelUIKit.SetRole(jobButton, ButtonRole.Neutral);
            PixelUIKit.SetText(jobPanelText, "\"No job for you today.\"\n\n<color=#9FB0CC>" + (string.IsNullOrEmpty(why) ? "He has nothing to deliver this visit - maybe next time." : why) + "</color>");
            jobAccept.gameObject.SetActive(false);
        }
    }

    /// <summary>Swaps the wares window for the job window (and back).</summary>
    private void ShowJobPanel(bool show)
    {
        if (jobPanel == null || waresPanel == null) return;
        RefreshJobParts();
        PixelAudio.Play("ui_click");
        waresPanel.gameObject.SetActive(!show);
        jobPanel.gameObject.SetActive(show);
        if (show) PixelPop.Show(jobPanel.gameObject);
        else PixelPop.Show(waresPanel.gameObject);
    }

    private void AcceptJob()
    {
        PixelDeliveryJobs jobs = PixelDeliveryJobs.Instance;
        if (jobs == null || jobOffer == null) return;
        if (jobs.Accept(jobOffer)) PixelAudio.Play("purchase");
        jobOffer = null;
        ShowJobPanel(false);
    }
}
