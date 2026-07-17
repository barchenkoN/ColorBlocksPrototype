#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *CB_LightGenerator;
static UIImpactFeedbackGenerator *CB_SoftGenerator;
static UIImpactFeedbackGenerator *CB_HeavyGenerator;

static UIImpactFeedbackGenerator *CB_GetGenerator(UIImpactFeedbackStyle style)
{
    UIImpactFeedbackGenerator *generator = nil;
    if (style == UIImpactFeedbackStyleHeavy)
    {
        if (CB_HeavyGenerator == nil)
            CB_HeavyGenerator = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
        generator = CB_HeavyGenerator;
    }
    if (@available(iOS 13.0, *))
    {
        if (style == UIImpactFeedbackStyleSoft)
        {
            if (CB_SoftGenerator == nil)
                CB_SoftGenerator = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
            generator = CB_SoftGenerator;
        }
    }

    if (generator == nil)
    {
        if (CB_LightGenerator == nil)
            CB_LightGenerator = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
        generator = CB_LightGenerator;
    }

    [generator prepare];
    return generator;
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
