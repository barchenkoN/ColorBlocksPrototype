#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *CB_LightGenerator;
static UIImpactFeedbackGenerator *CB_SoftGenerator;
static UIImpactFeedbackGenerator *CB_HeavyGenerator;

static UIImpactFeedbackGenerator *CB_GetGenerator(UIImpactFeedbackStyle style)
{
    UIImpactFeedbackGenerator **slot = &CB_LightGenerator;
    if (style == UIImpactFeedbackStyleHeavy) slot = &CB_HeavyGenerator;
    if (@available(iOS 13.0, *))
    {
        if (style == UIImpactFeedbackStyleSoft) slot = &CB_SoftGenerator;
    }

    if (*slot == nil) *slot = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
    [*slot prepare];
    return *slot;
}

extern "C"
{
    void CB_HapticLight()
    {
        [CB_GetGenerator(UIImpactFeedbackStyleLight) impactOccurred];
    }

    void CB_HapticSoft()
    {
        if (@available(iOS 13.0, *))
        {
            [CB_GetGenerator(UIImpactFeedbackStyleSoft) impactOccurred];
        }
        else
        {
            [CB_GetGenerator(UIImpactFeedbackStyleLight) impactOccurred];
        }
    }

    void CB_HapticHeavy()
    {
        [CB_GetGenerator(UIImpactFeedbackStyleHeavy) impactOccurred];
    }
}
