using System;
using System.Linq;
using TalentLibrary;
class RulesTests {
 static int n;
 static void Check(bool ok,string label){n++;if(!ok)throw new Exception(label);}
 static SkillState S(int id,int passion,int level,bool disabled=false,float remaining=1000){return new SkillState(id,passion,level,disabled,remaining);}
 static void Main(){
  var a=new[]{S(0,2,12),S(1,1,4),S(2,0,2)};
  Check(LearningRules.SelectStage(a)==2,"double fire first");
  var p=LearningRules.Allocate(a,1,0,17.5f);Check(p.Count==1&&p[0].Id==0&&p[0].XP==20,"major 20XP");
  a=new[]{S(0,2,18),S(1,1,4),S(2,0,2)};
  Check(LearningRules.SelectStage(a)==1,"single after major complete");
  p=LearningRules.Allocate(a,1,0,17);Check(p.Single().Id==1&&p.Single().XP==15,"minor 15XP");
  a=new[]{S(0,2,18),S(1,1,18),S(2,0,2),S(3,0,8),S(4,0,10)};
  p=LearningRules.Allocate(a,1,0,20);Check(p.Count==2&&p.All(x=>x.XP==10),"general pool evenly divided below10");
  a=new[]{S(0,0,2),S(1,0,3)};p=LearningRules.Allocate(a,2,0,20);Check(p.All(x=>x.XP==20),"no passion direct general and quality");
  a=new[]{S(0,2,1,true),S(1,1,2)};Check(LearningRules.SelectStage(a)==1,"disabled major cannot block");
  a=new[]{S(0,2,17,false,3)};p=LearningRules.Allocate(a,2,0,20);Check(p.Single().XP==3,"18 hard cap");
  a=new[]{S(0,0,9,false,1),S(1,0,9)};p=LearningRules.Allocate(a,1,0,20);Check(p[0].XP==1&&p[1].XP==10,"10 hard cap no redistribution");
  a=new[]{S(0,2,17),S(1,2,17)};p=LearningRules.Allocate(a,1,1,20);Check(p.Single().Id==1,"random index selects eligible major");
  a=new[]{S(0,2,17),S(1,1,18),S(2,0,3)};Check(LearningRules.SelectStage(a)==2,"decay returns major priority");
  a=new[]{S(0,0,10),S(1,2,18),S(2,1,18)};Check(LearningRules.SelectStage(a)==-1,"finished no job");
  Check(Math.Abs(LearningRules.Knowledge(16,new float[]{18,15,14,13,4})-15.5)<0.001,"knowledge formula");
  float[] thresholds={0,3.9f,4,7.9f,8,11.9f,12,14.9f,15,17.9f,18,20};int[] caps={1,1,2,2,3,3,4,4,5,5,6,6};
  for(int i=0;i<thresholds.Length;i++)Check(LearningRules.QualityCap(thresholds[i])==caps[i],"quality boundary "+thresholds[i]);
  for(int i=0;i<7;i++)Check(Math.Abs(LearningRules.QualityMultiplier(i)-(0.5f+i*0.25f))<0.001,"quality multiplier");
  Check(LearningRules.DailyFactor(3999)==1&&LearningRules.DailyFactor(4000)==0.2f,"daily saturation");
  a=new[]{new SkillState(0,2,17,false,3,4000)};p=LearningRules.Allocate(a,1,0,20);Check(p.Single().XP==3,"saturation before cap reaches18");
  Console.WriteLine("PASS "+n+" rule assertions");
 }
}
