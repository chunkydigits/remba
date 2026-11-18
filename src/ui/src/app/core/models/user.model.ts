export interface User {
  id: string;
  username: string;
  displayName: string;
  email: string;
  profile: UserProfile;
  socialProfiles: SocialProfile[];
  added: string;
  edited: string | null;
}

export interface UserProfile {
  bio: string;
  image: string;
  visibility: 'private' | 'public' | string;
}

export interface SocialProfile {
  platform: string;
  url: string;
}

export interface UpdateUserRequest {
  displayName?: string;
  username?: string;
  profile?: {
    bio?: string;
    image?: string;
    visibility?: string;
  };
  socialProfiles?: SocialProfile[];
}
