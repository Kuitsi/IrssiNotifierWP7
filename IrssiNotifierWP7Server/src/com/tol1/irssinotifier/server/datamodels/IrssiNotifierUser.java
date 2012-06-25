package com.tol1.irssinotifier.server.datamodels;

import javax.persistence.*;

import com.googlecode.objectify.annotation.Unindexed;

public class IrssiNotifierUser {
	@Id public String UserID;
	public String UUID;
	@Unindexed public String ChannelURI;
	@Unindexed public String guid;
	@Unindexed public boolean sendToastNotifications;
	@Unindexed public boolean sendTileNotifications;
	@Unindexed public boolean errorOccurred;
	@Unindexed public int tileCount;
	@Unindexed public int toastNotificationInterval;
	@Unindexed public long lastToastNotificationSent;
	
	public IrssiNotifierUser(){}
	
	public IrssiNotifierUser(String id, String guid, String uuid){
		this.UserID = id;
		this.guid = guid;
		this.UUID = uuid;
		this.toastNotificationInterval = 15;
	}
}
