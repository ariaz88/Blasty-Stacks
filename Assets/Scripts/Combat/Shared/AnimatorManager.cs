using UnityEngine;

public class AnimatorManager : MonoBehaviour
{
    public Animator anim;
    public void PlayTargetAnimation(string targetAnim , bool isInteracting  , float speedMult = 1f)
    {
        anim.applyRootMotion = isInteracting;
        anim.SetBool("isInteracting" , isInteracting);
        anim.SetFloat("ClipSpeed", speedMult);
        anim.CrossFade(targetAnim , 0.2f);
    }
 
}
