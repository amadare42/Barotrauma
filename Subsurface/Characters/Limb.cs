﻿using System;
using System.Xml.Linq;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Joints;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Subsurface
{
    public enum LimbType
    {
        None, LeftHand, RightHand, LeftArm, RightArm,
        LeftLeg, RightLeg, LeftFoot, RightFoot, Head, Torso, Tail, Legs, RightThigh, LeftThigh, Waist
    };

    class Limb
    {
        private const float LimbDensity = 15;
        private const float LimbAngularDamping = 7;

        public readonly Character character;
        
        //the physics body of the limb
        public PhysicsBody body;
        private Texture2D bodyShapeTexture;

        private readonly int refJointIndex;

        private readonly float steerForce;

        private readonly bool doesFlip;
        
        public Sprite sprite;

        public bool inWater;

        public FixedMouseJoint pullJoint;

        public readonly LimbType type;

        public readonly bool ignoreCollisions;

        private float maxHealth;
        private float damage;
        private float bleeding;

        public readonly float impactTolerance;

        Sound hitSound;
        //a timer for delaying when a hitsound/attacksound can be played again
        public float soundTimer;
        public const float SoundInterval = 0.2f;

        public readonly Attack attack;

        private Direction dir;

        private Item wearingItem;
        private Sprite wearingItemSprite;

        private Vector2 animTargetPos;

        public Texture2D BodyShapeTexture
        {
            get { return bodyShapeTexture; }
        }
        
        public bool DoesFlip
        {
            get { return doesFlip; }
        }

        public Vector2 SimPosition
        {
            get { return body.Position; }
        }

        public float Rotation
        {
            get { return body.Rotation; }
        }

        public Vector2 AnimTargetPos
        {
            get { return animTargetPos; }
        }

        public float SteerForce
        {
            get { return steerForce; }
        }

        public float Mass
        {
            get { return body.Mass; }
        }

        public bool Disabled { get; set; }

        public Sound HitSound
        {
            get { return hitSound; }
        }
                
        public Vector2 LinearVelocity
        {
            get { return body.LinearVelocity; }
        }

        public float Dir
        {
            get { return ((dir == Direction.Left) ? -1.0f : 1.0f); }
            set { dir = (value==-1.0f) ? Direction.Left : Direction.Right; }
        }

        public int RefJointIndex
        {
            get { return refJointIndex; }
        }

        public float Damage
        {
            get { return damage; }
            set 
            { 
                damage = Math.Max(value, 0.0f);
                if (damage >=maxHealth) character.Kill();
            }
        }

        public float Bleeding
        {
            get { return bleeding; }
            set { bleeding = MathHelper.Clamp(value, 0.0f, 100.0f); }
        }

        public Item WearingItem
        {
            get { return wearingItem; }
            set { wearingItem = value; }
        }

        public Sprite WearingItemSprite
        {
            get { return wearingItemSprite; }
            set { wearingItemSprite = value; }
        }

        public Limb (Character character, XElement element)
        {
            this.character = character;
            
            dir = Direction.Right;

            doesFlip = ToolBox.GetAttributeBool(element, "flip", false);

            body = new PhysicsBody(element);

            if (ToolBox.GetAttributeBool(element, "ignorecollisions", false))
            {
                body.CollisionCategories = Category.None;
                body.CollidesWith = Category.None;

                ignoreCollisions = true;
            }
            else
            {
                //limbs don't collide with each other
                body.CollisionCategories = Physics.CollisionCharacter;
                body.CollidesWith = Physics.CollisionAll & ~Physics.CollisionCharacter & ~Physics.CollisionMisc;
            }

            impactTolerance = ToolBox.GetAttributeFloat(element, "impacttolerance", 20.0f);

            body.UserData = this;

            refJointIndex = -1;

            if (element.Attribute("type") != null)
            {
                type = (LimbType)Enum.Parse(typeof(LimbType), element.Attribute("type").Value, true);

                Vector2 jointPos = ToolBox.GetAttributeVector2(element, "pullpos", Vector2.Zero);

                jointPos = ConvertUnits.ToSimUnits(jointPos);

                refJointIndex = ToolBox.GetAttributeInt(element, "refjoint", -1);

                pullJoint = new FixedMouseJoint(body.FarseerBody, jointPos);
                pullJoint.Enabled = false;
                pullJoint.MaxForce = 150.0f * body.Mass;

                Game1.world.AddJoint(pullJoint);
            }
            else
            {
                type = LimbType.None;
            }

            steerForce = ToolBox.GetAttributeFloat(element, "steerforce", 0.0f);

            maxHealth = Math.Max(ToolBox.GetAttributeFloat(element, "health", 100.0f),1.0f);
            
            body.BodyType = BodyType.Dynamic;
            body.FarseerBody.AngularDamping = LimbAngularDamping;

            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString())
                {
                    case "sprite":
                        string spritePath = subElement.Attribute("texture").Value;

                        if (character.info!=null)
                            spritePath = spritePath.Replace("[GENDER]", (character.info.gender == Gender.Female) ? "f" : "");

                        sprite = new Sprite(subElement, "", spritePath);
                        break;
                    case "attack":
                        attack = new Attack(subElement);
                        break;
                    case "sound":
                        hitSound = Sound.Load(ToolBox.GetAttributeString(subElement, "file", ""));
                        break;
                }
            }
        }

        public void Move(Vector2 pos, float amount, bool pullFromCenter=false)
        {
            Vector2 pullPos = body.Position;
            if (pullJoint!=null && !pullFromCenter)
            {
                pullPos = pullJoint.WorldAnchorA;
            }

            animTargetPos = pos;

            Vector2 vel = body.LinearVelocity;
            Vector2 deltaPos = pos - pullPos;
            deltaPos *= amount;
            body.ApplyLinearImpulse((deltaPos - vel * 0.5f) * body.Mass, pullPos);
        }

        public void Update(float deltaTime)
        {
            if (LinearVelocity.X>100.0f)
            {
                DebugConsole.ThrowError("CHARACTER EXPLODED");
                foreach (Limb limb in character.animController.limbs)
                {
                    limb.body.ResetDynamics();
                    limb.body.SetTransform(body.Position, 0.0f);
                }                
            }

            if (inWater)
            {
                //buoyancy
                Vector2 buoyancy = new Vector2(0, Mass * 9.6f);

                //drag
                Vector2 velDir = Vector2.Normalize(LinearVelocity);

                Vector2 line = new Vector2((float)Math.Cos(body.Rotation), (float)Math.Sin(body.Rotation));
                line *= ConvertUnits.ToSimUnits(sprite.size.Y);

                Vector2 normal = new Vector2(-line.Y, line.X);
                normal = Vector2.Normalize(-normal);

                float dragDot = Vector2.Dot(normal, velDir);
                Vector2 dragForce = Vector2.Zero;
                if (dragDot > 0)
                {
                    float vel = LinearVelocity.Length();
                    float drag = dragDot * vel * vel
                        * ConvertUnits.ToSimUnits(sprite.size.Y);
                    dragForce = drag * -velDir;
                    if (dragForce.Length() > 100.0f) { }
                }

                body.ApplyForce(dragForce + buoyancy);
                body.ApplyTorque(body.AngularVelocity * body.Mass * -0.05f);
            }

            if (character.IsDead) return;

            soundTimer -= deltaTime;

            if (ToolBox.RandomFloat(0.0f, 1000.0f) < Bleeding)
            {
                Game1.particleManager.CreateParticle(SimPosition,
                    MathHelper.Pi,
                    ToolBox.RandomFloat(0.0f, 0.0f), !inWater ? "blood" : "waterblood");
            }
        }


        public void Draw(SpriteBatch spriteBatch, bool debugDraw)
        {
            Color color = new Color(1.0f, 1.0f - damage / maxHealth, 1.0f - damage / maxHealth);

            body.Dir = Dir;
            body.Draw(spriteBatch, sprite, color);
            
            if (wearingItem != null)
            {
                SpriteEffects spriteEffect = (dir == Direction.Right) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                wearingItemSprite.Draw(spriteBatch,
                    new Vector2(body.DrawPosition.X, -body.DrawPosition.Y),
                    color,
                    -body.DrawRotation,
                    1.0f, spriteEffect);
            }

            if (!debugDraw) return;

            if (pullJoint!=null)
            {
                Vector2 pos = ConvertUnits.ToDisplayUnits(pullJoint.WorldAnchorB);
                GUI.DrawRectangle(spriteBatch, new Rectangle((int)pos.X, (int)pos.Y, 5, 5), Color.Red, true);
            }

            if (bodyShapeTexture == null)
            {
                switch (body.bodyShape)
                {
                    case PhysicsBody.Shape.Rectangle:
                        bodyShapeTexture = GUI.CreateRectangle(
                            (int)ConvertUnits.ToDisplayUnits(body.width), 
                            (int)ConvertUnits.ToDisplayUnits(body.height));
                        break;

                    case PhysicsBody.Shape.Capsule:
                        bodyShapeTexture = GUI.CreateCapsule(
                            (int)ConvertUnits.ToDisplayUnits(body.radius),
                            (int)ConvertUnits.ToDisplayUnits(body.height));
                        break;
                    case PhysicsBody.Shape.Circle:
                        bodyShapeTexture = GUI.CreateCircle((int)ConvertUnits.ToDisplayUnits(body.radius));
                        break;
                }
            }
            spriteBatch.Draw(
                bodyShapeTexture,
                new Vector2(body.DrawPosition.X, -body.DrawPosition.Y),
                null,
                Color.White,
                -body.DrawRotation,
                new Vector2(bodyShapeTexture.Width / 2, bodyShapeTexture.Height / 2), 1.0f, SpriteEffects.None, 0.0f);
        }
        

        public void Remove()
        {
            sprite.Remove();
            body.Remove();
            if (hitSound!=null) hitSound.Remove();
        }
    }
}
